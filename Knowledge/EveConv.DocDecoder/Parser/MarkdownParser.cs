using EveConv.Abstraction;
using EveConv.Abstraction.DocParser.Block;
using EveConv.Abstraction.DocParser.Block.Inline;
using Markdig;
using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace EveConv.DocDecoder.Parser;

public sealed class MarkdownParser : DocParseable
{
    private const string MarkdownMimeType = "text/markdown";
    private const string MarkdownMimeTypeAlt = "text/x-markdown";
    private const string MarkdownAppMimeType = "application/markdown";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public MarkdownParser(IMimeTypeDetection mimeTypeDetection) : base(mimeTypeDetection)
    {
    }

    public override int Priority => DEFAULT_PRIORITY + 10;

    public override bool Accept(string fileType)
    {
        if (string.IsNullOrWhiteSpace(fileType))
        {
            return false;
        }

        var normalized = fileType.Split(';', 2)[0].Trim();
        return string.Equals(normalized, MarkdownMimeType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, MarkdownMimeTypeAlt, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, MarkdownAppMimeType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, ".md", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "md", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, ".markdown", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "markdown", StringComparison.OrdinalIgnoreCase);
    }

    protected override async Task<(IEnumerable<IParagraphBlock> Paragraphs, IEnumerable<SectionBlock> Sections)> ParseAsync(
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(fileStream, leaveOpen: true);
        var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
        }

        var normalized = Markdown.Normalize(content);
        var markdownDocument = Markdown.Parse(normalized, Pipeline);

        var rootParagraphs = new List<IParagraphBlock>();
        var rootSections = new List<SectionDraft>();
        var sectionStack = new Stack<SectionDraft>();
        var sectionCounters = new int[6];

        foreach (var block in markdownDocument)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (block is HeadingBlock heading)
            {
                var level = Math.Clamp(heading.Level, 1, 6);
                var title = GetInlineText(heading.Inline).Trim();
                if (!string.IsNullOrWhiteSpace(title))
                {
                    PushSection(level, title, GetLine(block));
                }
                continue;
            }

            var paragraph = ConvertBlockToParagraph(block);
            if (paragraph is not null)
            {
                AddParagraph(paragraph);
            }
        }

        var documentEndLine = markdownDocument.Count > 0
            ? GetLine(markdownDocument.Last()) + 1
            : 1;

        while (sectionStack.TryPop(out var section))
        {
            section.LineEnd = Math.Max(section.LineEnd, documentEndLine);
        }

        return (rootParagraphs, rootSections.Select(section => section.ToSectionBlock()));

        void PushSection(int level, string title, int lineNumber)
        {
            while (sectionStack.Count > 0 && sectionStack.Peek().Level >= level)
            {
                var closed = sectionStack.Pop();
                closed.LineEnd = lineNumber - 1;
            }

            sectionCounters[level - 1]++;
            for (var idx = level; idx < sectionCounters.Length; idx++)
            {
                sectionCounters[idx] = 0;
            }

            var numbering = string.Join('.', sectionCounters.Take(level).Where(number => number > 0));
            var section = new SectionDraft(level, numbering, title, lineNumber);

            if (sectionStack.Count == 0)
            {
                rootSections.Add(section);
            }
            else
            {
                sectionStack.Peek().SubSections.Add(section);
            }

            sectionStack.Push(section);
        }

        void AddParagraph(IParagraphBlock paragraph)
        {
            if (sectionStack.Count == 0)
            {
                rootParagraphs.Add(paragraph);
                return;
            }

            sectionStack.Peek().Paragraphs.Add(paragraph);
        }
    }

    private static IParagraphBlock? ConvertBlockToParagraph(Block block)
    {
        if (block is ParagraphBlock paragraphBlock)
        {
            var inlines = ConvertInline(paragraphBlock.Inline).ToList();
            if (inlines.Count == 0)
            {
                return null;
            }

            return new PlainTextBlock(inlines)
            {
                LineStart = GetLine(block),
                LineEnd = GetLine(block),
            };
        }

        if (block is FencedCodeBlock fencedCodeBlock)
        {
            var codeText = GetLeafLines(fencedCodeBlock);
            var codeParagraph = new FormattedBlock(codeText)
            {
                Formatting = "code",
                LineStart = GetLine(block),
                LineEnd = GetLine(block),
            };

            if (!string.IsNullOrWhiteSpace(fencedCodeBlock.Info))
            {
                codeParagraph.Properties["Language"] = fencedCodeBlock.Info;
            }

            return codeParagraph;
        }

        if (block is Markdig.Syntax.ListBlock listBlock)
        {
            var items = new List<IParagraphBlock>();

            foreach (var child in listBlock)
            {
                if (child is not ListItemBlock listItem)
                {
                    continue;
                }

                var itemParagraphs = listItem
                    .Select(ConvertBlockToParagraph)
                    .Where(paragraph => paragraph is not null)
                    .Cast<IParagraphBlock>()
                    .ToList();

                if (itemParagraphs.Count == 0)
                {
                    continue;
                }

                items.Add(itemParagraphs.Count == 1
                    ? itemParagraphs[0]
                    : new RichTextBlock(itemParagraphs, "markdown-list-item")
                    {
                        LineStart = GetLine(listItem),
                        LineEnd = GetLine(listItem),
                    });
            }

            if (items.Count == 0)
            {
                return null;
            }

            return new EveConv.Abstraction.DocParser.Block.ListBlock<IParagraphBlock>(items)
            {
                ListType = listBlock.IsOrdered ? "ordered" : "unordered",
                LineStart = GetLine(listBlock),
                LineEnd = GetLine(listBlock),
            };
        }

        if (block is QuoteBlock quoteBlock)
        {
            var quoteParagraphs = quoteBlock
                .Select(ConvertBlockToParagraph)
                .Where(paragraph => paragraph is not null)
                .Cast<IParagraphBlock>()
                .ToList();

            if (quoteParagraphs.Count == 0)
            {
                return null;
            }

            return new RichTextBlock(quoteParagraphs, "blockquote")
            {
                LineStart = GetLine(quoteBlock),
                LineEnd = GetLine(quoteBlock),
            };
        }

        if (block is ThematicBreakBlock)
        {
            return new PlainTextBlock("---")
            {
                LineStart = GetLine(block),
                LineEnd = GetLine(block),
            };
        }

        if (block is LeafBlock leaf)
        {
            var text = GetLeafLines(leaf).Trim();
            if (text.Length == 0)
            {
                return null;
            }

            return new PlainTextBlock(text)
            {
                LineStart = GetLine(block),
                LineEnd = GetLine(block),
            };
        }

        return null;
    }

    private static IEnumerable<DocumentInlineBlock> ConvertInline(ContainerInline? container)
    {
        if (container is null)
        {
            return [];
        }

        var inlines = new List<DocumentInlineBlock>();
        var charIndex = 0;

        for (var inline = container.FirstChild; inline is not null; inline = inline.NextSibling)
        {
            switch (inline)
            {
                case LiteralInline literal:
                {
                    var text = literal.Content.ToString();
                    AddInlineText(text, inlines, ref charIndex);
                    break;
                }
                case CodeInline codeInline:
                {
                    AddFormattedText(codeInline.Content, "code", inlines, ref charIndex);
                    break;
                }
                case LineBreakInline:
                {
                    AddInlineText(Environment.NewLine, inlines, ref charIndex);
                    break;
                }
                case LinkInline linkInline when !linkInline.IsImage:
                {
                    var text = GetInlineText(linkInline);
                    AddInlineLink(text, linkInline.GetDynamicUrl != null ? linkInline.GetDynamicUrl() ?? string.Empty : linkInline.Url ?? string.Empty, inlines, ref charIndex);
                    break;
                }
                case EmphasisInline emphasisInline:
                {
                    var text = GetInlineText(emphasisInline);
                    if (string.IsNullOrEmpty(text))
                    {
                        break;
                    }

                    var formatting = emphasisInline.DelimiterCount >= 2 ? "strong" : "emphasis";
                    AddFormattedText(text, formatting, inlines, ref charIndex);
                    break;
                }
                case HtmlInline htmlInline:
                {
                    AddFormattedText(htmlInline.Tag, "html-inline", inlines, ref charIndex);
                    break;
                }
                default:
                {
                    if (inline is ContainerInline nested)
                    {
                        foreach (var nestedInline in ConvertInline(nested))
                        {
                            var adjustedInline = nestedInline with
                            {
                                CharStart = nestedInline.CharStart + charIndex,
                                CharEnd = nestedInline.CharEnd + charIndex,
                            };
                            inlines.Add(adjustedInline);
                        }

                        if (inlines.Count > 0)
                        {
                            charIndex = inlines[^1].CharEnd + 1;
                        }
                    }

                    break;
                }
            }
        }

        return inlines;
    }

    private static int GetLine(MarkdownObject obj)
    {
        return Math.Max(1, obj.Line + 1);
    }

    private static string GetLeafLines(LeafBlock block)
    {
        if (block.Lines.Count == 0)
        {
            return string.Empty;
        }

        var writer = new StringWriter();
        for (var i = 0; i < block.Lines.Count; i++)
        {
            var line = block.Lines.Lines[i];
            writer.Write(line.Slice.ToString());
            if (i < block.Lines.Count - 1)
            {
                writer.WriteLine();
            }
        }

        return writer.ToString();
    }

    private static string GetInlineText(ContainerInline? inline)
    {
        if (inline is null)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        AppendInlineText(inline, builder);
        return builder.ToString();
    }

    private static void AppendInlineText(ContainerInline container, System.Text.StringBuilder builder)
    {
        for (var inline = container.FirstChild; inline is not null; inline = inline.NextSibling)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    builder.Append(literal.Content.ToString());
                    break;
                case CodeInline codeInline:
                    builder.Append(codeInline.Content);
                    break;
                case LineBreakInline:
                    builder.AppendLine();
                    break;
                case LinkInline linkInline:
                    AppendInlineText(linkInline, builder);
                    break;
                case EmphasisInline emphasisInline:
                    AppendInlineText(emphasisInline, builder);
                    break;
                case HtmlInline htmlInline:
                    builder.Append(htmlInline.Tag);
                    break;
                case ContainerInline nested:
                    AppendInlineText(nested, builder);
                    break;
            }
        }
    }

    private static void AddInlineText(string text, List<DocumentInlineBlock> inlines, ref int charIndex)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        inlines.Add(new InlineTextBlock(text)
        {
            CharStart = charIndex,
            CharEnd = charIndex + text.Length - 1,
        });

        charIndex += text.Length;
    }

    private static void AddFormattedText(string text, string formatting, List<DocumentInlineBlock> inlines, ref int charIndex)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        inlines.Add(new InlineFormattedBlock(text)
        {
            Formatting = formatting,
            CharStart = charIndex,
            CharEnd = charIndex + text.Length - 1,
        });

        charIndex += text.Length;
    }

    private static void AddInlineLink(string text, string uri, List<DocumentInlineBlock> inlines, ref int charIndex)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(uri))
        {
            return;
        }

        inlines.Add(new MarkdownInlineLinkBlock(text)
        {
            Uri = uri,
            CharStart = charIndex,
            CharEnd = charIndex + text.Length - 1,
        });

        charIndex += text.Length;
    }

    private sealed class SectionDraft
    {
        public int Level { get; }
        public string Numbering { get; }
        public string Title { get; }
        public int LineStart { get; }
        public int LineEnd { get; set; }
        public List<IParagraphBlock> Paragraphs { get; } = [];
        public List<SectionDraft> SubSections { get; } = [];

        public SectionDraft(int level, string numbering, string title, int lineStart)
        {
            Level = level;
            Numbering = numbering;
            Title = title;
            LineStart = lineStart;
            LineEnd = lineStart;
        }

        public SectionBlock ToSectionBlock()
        {
            return new SectionBlock(Numbering, Title)
            {
                LineStart = LineStart,
                LineEnd = LineEnd,
                Paragraphs = Paragraphs,
                SubSections = SubSections.Select(section => section.ToSectionBlock()).ToList(),
            };
        }
    }

    private sealed record MarkdownInlineLinkBlock : InlineLinkBlock
    {
        public MarkdownInlineLinkBlock(string text) : base(text)
        {
        }
    }
}
