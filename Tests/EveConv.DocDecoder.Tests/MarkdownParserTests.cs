using System.Text;
using EveConv.Abstraction;
using EveConv.Abstraction.DocParser.Block;
using EveConv.Abstraction.DocParser.Block.Inline;
using EveConv.DocDecoder.Parser;
using EveConv.Downloader;

namespace EveConv.DocDecoder.Tests;

public class MarkdownParserTests
{
    private const string MarkdownMimeType = "text/markdown";

    [Theory]
    [InlineData("md")]
    [InlineData(".md")]
    [InlineData("markdown")]
    [InlineData(".markdown")]
    [InlineData(MarkdownMimeType)]
    [InlineData(MarkdownMimeType + "; charset=utf-8")]
    public void Accept_ReturnsTrueForMarkdownTypes(string fileType)
    {
        var mimeTypeDetection = new TestMimeTypeDetection();
        var parser = new MarkdownParser(mimeTypeDetection, new LocalDownloader(mimeTypeDetection));

        Assert.True(parser.Accept(fileType));
    }

    [Fact]
    public async Task ParseAsync_ExtractsSectionsLinksAndCodeBlocks()
    {
        const string markdown = """
            intro line
            second line

            # Root Title
            Paragraph with [docs](https://example.com/docs) and `snippet`.

            ## Child Title
            ```csharp
            Console.WriteLine("Hi");
            ```
            """;
        await File.WriteAllBytesAsync("sample.md", Encoding.UTF8.GetBytes(markdown), TestContext.Current.CancellationToken);
        var mimeTypeDetection = new TestMimeTypeDetection();
        var parser = new MarkdownParser(mimeTypeDetection, new LocalDownloader(mimeTypeDetection));

        var document = await parser.ParseAsync("sample.md", TestContext.Current.CancellationToken);

        Assert.Equal(MarkdownMimeType, document.DocType);

        var rootParagraph = Assert.IsType<PlainTextBlock>(Assert.Single(document.Paragraphs));
        Assert.Equal("intro line" + Environment.NewLine + "second line", GetText(rootParagraph));

        var rootSection = Assert.Single(document.Sections);
        Assert.Equal("1", rootSection.Numbering);
        Assert.Equal("Root Title", rootSection.Title);

        var sectionParagraph = Assert.IsType<PlainTextBlock>(Assert.Single(rootSection.Paragraphs));
        var link = sectionParagraph.Content.OfType<InlineLinkBlock>().Single();
        Assert.Equal("docs", link.Text);
        Assert.Equal("https://example.com/docs", link.Uri);
        Assert.Contains(sectionParagraph.Content.OfType<InlineFormattedBlock>(), inline =>
            inline.Formatting == "code" && inline.Text == "snippet");

        var childSection = Assert.Single(rootSection.SubSections);
        Assert.Equal("1.1", childSection.Numbering);
        Assert.Equal("Child Title", childSection.Title);

        var codeBlock = Assert.IsType<FormattedBlock>(Assert.Single(childSection.Paragraphs));
        Assert.Equal("code", codeBlock.Formatting);
        Assert.Equal("Console.WriteLine(\"Hi\");", GetText(codeBlock));
        Assert.Equal("csharp", codeBlock.Properties["Language"]);
    }

    [Fact]
    public async Task DocumentParser_UsesMarkdownParserBeforeTxtParser()
    {
        const string markdown = """
            # Title
            body
            """;
        await File.WriteAllBytesAsync("sample.md", Encoding.UTF8.GetBytes(markdown), TestContext.Current.CancellationToken);
        var mimeTypeDetection = new TestMimeTypeDetection();
        var downloader = new LocalDownloader(mimeTypeDetection);
        var parser = new DocumentParser([new TxtParser(mimeTypeDetection, downloader), new MarkdownParser(mimeTypeDetection, downloader)], mimeTypeDetection);

        var document = await parser.ParseAsync("sample.md", TestContext.Current.CancellationToken);

        var section = Assert.Single(document.Sections);
        Assert.Equal("Title", section.Title);
    }

    private static string GetText(IParagraphBlock block)
    {
        return block switch
        {
            PlainTextBlock plainText => string.Concat(plainText.Content
                .OfType<InlineTextBlock>()
                .Select(inline => inline.Text)),
            FormattedBlock formatted => string.Concat(formatted.Content
                .OfType<InlineTextBlock>()
                .Select(inline => inline.Text)),
            RichTextBlock richText => string.Join(Environment.NewLine, richText.Content.Select(GetText)),
            _ => string.Empty,
        };
    }

    private sealed class TestMimeTypeDetection : IMimeTypeDetection
    {
        public string GetFileType(string filename)
        {
            return Path.GetExtension(filename) switch
            {
                ".md" => MarkdownMimeType,
                ".markdown" => MarkdownMimeType,
                _ => "text/plain",
            };
        }
    }
}
