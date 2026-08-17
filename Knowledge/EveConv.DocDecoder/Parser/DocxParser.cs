using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using EveConv.Abstraction;
using EveConv.Abstraction.DocParser.Block;
using EveConv.Abstraction.DocParser.Block.Inline;

namespace EveConv.DocDecoder.Parser;

public class DocxParser : DocParseable
{
    private const string DocxMimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace Cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Dcterms = "http://purl.org/dc/terms/";
    private static readonly XNamespace Ep = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";
    private static readonly XNamespace Custom = "http://schemas.openxmlformats.org/officeDocument/2006/custom-properties";

    public DocxParser(IMimeTypeDetection mimeTypeDetection) : base(mimeTypeDetection)
    {
    }

    public override bool Accept(string fileType)
    {
        if (string.IsNullOrWhiteSpace(fileType))
        {
            return false;
        }

        var normalized = fileType.Split(';', 2)[0].Trim();
        return string.Equals(normalized, DocxMimeType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, ".docx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "docx", StringComparison.OrdinalIgnoreCase);
    }

    protected override Task<(IEnumerable<IParagraphBlock> Paragraphs, IEnumerable<SectionBlock> Sections)> ParseAsync(
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ResetStream(fileStream);

        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: true);
        var document = LoadXml(archive, "word/document.xml");
        if (document?.Root is null)
        {
            return Task.FromResult<(IEnumerable<IParagraphBlock>, IEnumerable<SectionBlock>)>(([], []));
        }

        var relationships = LoadRelationships(archive, "word/_rels/document.xml.rels");
        var paragraphs = new List<IParagraphBlock>();
        var lineNumber = 1;

        foreach (var element in document.Root.Descendants(W + "body").Elements())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element.Name == W + "p")
            {
                var paragraph = ParseParagraph(element, relationships, lineNumber);
                if (!IsEmpty(paragraph))
                {
                    paragraphs.Add(paragraph);
                }
                lineNumber++;
            }
            else if (element.Name == W + "tbl")
            {
                var table = ParseTable(element, relationships, ref lineNumber);
                if (table is not null)
                {
                    paragraphs.Add(table);
                }
            }
        }

        ResetStream(fileStream);
        return Task.FromResult<(IEnumerable<IParagraphBlock>, IEnumerable<SectionBlock>)>((paragraphs, []));
    }

    protected override async Task<Dictionary<string, string?>> GetMetadataAsync(
        string source,
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var baseMetadata = await GetFileSystemMetadataAsync(source, fileStream).ConfigureAwait(false);
        var metadata = new DocxMetadata(baseMetadata);
        ResetStream(fileStream);

        try
        {
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: true);
            FillCoreMetadata(metadata, LoadXml(archive, "docProps/core.xml"));
            FillAppMetadata(metadata, LoadXml(archive, "docProps/app.xml"));
            FillCustomMetadata(metadata, LoadXml(archive, "docProps/custom.xml"));
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or IOException)
        {
        }

        ResetStream(fileStream);
        return metadata.ToDictionary();
    }

    private static PlainTextBlock ParseParagraph(
        XElement paragraphElement,
        IReadOnlyDictionary<string, string> relationships,
        int lineNumber)
    {
        var inlines = new List<DocumentInlineBlock>();
        var charIndex = 0;

        foreach (var child in paragraphElement.Elements())
        {
            ParseInlineContent(child, relationships, inlines, ref charIndex);
        }

        var properties = GetParagraphProperties(paragraphElement);
        var paragraph = new PlainTextBlock(inlines)
        {
            LineStart = lineNumber,
            LineEnd = lineNumber,
            Numbering = GetNumbering(properties),
            Properties = properties,
        };

        return paragraph;
    }

    private static void ParseInlineContent(
        XElement element,
        IReadOnlyDictionary<string, string> relationships,
        List<DocumentInlineBlock> inlines,
        ref int charIndex,
        string? hyperlinkUri = null)
    {
        if (element.Name == W + "hyperlink")
        {
            var relationshipId = (string?)element.Attribute(R + "id");
            var anchor = (string?)element.Attribute(W + "anchor");
            var uri = relationshipId is not null && relationships.TryGetValue(relationshipId, out var target)
                ? target
                : anchor is null ? null : "#" + anchor;

            foreach (var child in element.Elements())
            {
                ParseInlineContent(child, relationships, inlines, ref charIndex, uri ?? hyperlinkUri);
            }

            return;
        }

        if (element.Name == W + "r")
        {
            ParseRun(element, hyperlinkUri, inlines, ref charIndex);
            return;
        }

        foreach (var child in element.Elements())
        {
            ParseInlineContent(child, relationships, inlines, ref charIndex, hyperlinkUri);
        }
    }

    private static void ParseRun(
        XElement run,
        string? hyperlinkUri,
        List<DocumentInlineBlock> inlines,
        ref int charIndex)
    {
        var formatting = GetRunFormatting(run.Element(W + "rPr"));

        foreach (var child in run.Elements())
        {
            var text = child.Name switch
            {
                var name when name == W + "t" => child.Value,
                var name when name == W + "tab" => "\t",
                var name when name == W + "br" => "\n",
                var name when name == W + "cr" => "\n",
                var name when name == W + "noBreakHyphen" => "-",
                _ => null,
            };

            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            inlines.Add(CreateInline(text, charIndex, formatting, hyperlinkUri));
            charIndex += text.Length;
        }
    }

    private static DocumentInlineBlock CreateInline(string text, int charStart, string formatting, string? hyperlinkUri)
    {
        var charEnd = charStart + text.Length - 1;
        if (!string.IsNullOrWhiteSpace(hyperlinkUri))
        {
            return new DocxInlineLinkBlock(text)
            {
                Uri = hyperlinkUri,
                CharStart = charStart,
                CharEnd = charEnd,
            };
        }

        if (!string.IsNullOrWhiteSpace(formatting))
        {
            return new InlineFormattedBlock(text)
            {
                Formatting = formatting,
                CharStart = charStart,
                CharEnd = charEnd,
            };
        }

        return new InlineTextBlock(text)
        {
            CharStart = charStart,
            CharEnd = charEnd,
        };
    }

    private static TableBlock? ParseTable(
        XElement tableElement,
        IReadOnlyDictionary<string, string> relationships,
        ref int lineNumber)
    {
        var rows = new List<List<CellDraft>>();
        var verticalMerges = new Dictionary<int, CellDraft>();
        var rowCount = 0;

        foreach (var rowElement in tableElement.Elements(W + "tr"))
        {
            rowCount++;
            var row = new List<CellDraft>();
            var columnIndex = 0;

            foreach (var cellElement in rowElement.Elements(W + "tc"))
            {
                var properties = cellElement.Element(W + "tcPr");
                var colSpan = GetGridSpan(properties);
                var verticalMerge = properties?.Element(W + "vMerge");
                var isVerticalContinuation = verticalMerge is not null
                    && !string.Equals(GetVal(verticalMerge), "restart", StringComparison.OrdinalIgnoreCase);

                if (isVerticalContinuation)
                {
                    if (verticalMerges.TryGetValue(columnIndex, out var mergedCell))
                    {
                        mergedCell.RowSpan++;
                    }
                }
                else
                {
                    RemoveCoveredMerges(verticalMerges, columnIndex, colSpan);

                    var content = ParseCellContent(cellElement, relationships);
                    var cell = new CellDraft(content, colSpan);
                    row.Add(cell);

                    if (verticalMerge is not null)
                    {
                        for (var col = columnIndex; col < columnIndex + colSpan; col++)
                        {
                            verticalMerges[col] = cell;
                        }
                    }
                }

                columnIndex += colSpan;
            }

            rows.Add(row);
        }

        if (rows.Count == 0)
        {
            return null;
        }

        var table = new TableBlock(rows.Select(row => row.Select(cell => new TableCellBlock(
            cell.Content,
            cell.RowSpan,
            cell.ColSpan))))
        {
            LineStart = lineNumber,
            LineEnd = lineNumber + Math.Max(rowCount, 1) - 1,
        };

        lineNumber += Math.Max(rowCount, 1);
        return table;
    }

    private static IParagraphBlock ParseCellContent(
        XElement cellElement,
        IReadOnlyDictionary<string, string> relationships)
    {
        var paragraphs = cellElement
            .Elements(W + "p")
            .Select((paragraph, index) => ParseParagraph(paragraph, relationships, index + 1))
            .Where(paragraph => !IsEmpty(paragraph))
            .Cast<IParagraphBlock>()
            .ToList();

        return paragraphs.Count switch
        {
            0 => new PlainTextBlock(string.Empty),
            1 => paragraphs[0],
            _ => new RichTextBlock(paragraphs, "docx-cell"),
        };
    }

    private static Dictionary<string, string?> GetParagraphProperties(XElement paragraphElement)
    {
        var properties = new Dictionary<string, string?>();
        var paragraphProperties = paragraphElement.Element(W + "pPr");
        if (paragraphProperties is null)
        {
            return properties;
        }

        AddValue(properties, "Style", paragraphProperties.Element(W + "pStyle"));

        var numberingProperties = paragraphProperties.Element(W + "numPr");
        AddValue(properties, "NumberingLevel", numberingProperties?.Element(W + "ilvl"));
        AddValue(properties, "NumberingId", numberingProperties?.Element(W + "numId"));

        return properties;
    }

    private static string GetRunFormatting(XElement? runProperties)
    {
        if (runProperties is null)
        {
            return string.Empty;
        }

        var formats = new List<string>();
        AddOnOffFormat(formats, runProperties, "b", "bold");
        AddOnOffFormat(formats, runProperties, "i", "italic");
        AddOnOffFormat(formats, runProperties, "u", "underline");
        AddOnOffFormat(formats, runProperties, "strike", "strike");
        AddOnOffFormat(formats, runProperties, "dstrike", "double-strike");

        var verticalAlign = GetVal(runProperties.Element(W + "vertAlign"));
        if (!string.IsNullOrWhiteSpace(verticalAlign))
        {
            formats.Add(verticalAlign);
        }

        var style = GetVal(runProperties.Element(W + "rStyle"));
        if (!string.IsNullOrWhiteSpace(style))
        {
            formats.Add("style:" + style);
        }

        return string.Join(';', formats);
    }

    private static string GetNumbering(IReadOnlyDictionary<string, string?> properties)
    {
        var hasNumberingId = properties.TryGetValue("NumberingId", out var numberingId)
            && !string.IsNullOrWhiteSpace(numberingId);
        var hasLevel = properties.TryGetValue("NumberingLevel", out var level)
            && !string.IsNullOrWhiteSpace(level);

        return (hasNumberingId, hasLevel) switch
        {
            (true, true) => $"{numberingId}:{level}",
            (true, false) => numberingId!,
            _ => string.Empty,
        };
    }

    private static bool IsEmpty(PlainTextBlock paragraph)
    {
        return paragraph.Content.All(inline => inline is not InlineTextBlock textBlock
            || string.IsNullOrWhiteSpace(textBlock.Text));
    }

    private static void FillCoreMetadata(DocxMetadata metadata, XDocument? coreProperties)
    {
        if (coreProperties?.Root is null)
        {
            return;
        }

        metadata.Title = GetElementValue(coreProperties.Root, Dc + "title");
        metadata.Subject = GetElementValue(coreProperties.Root, Dc + "subject");
        metadata.Author = GetElementValue(coreProperties.Root, Dc + "creator");
        metadata.Description = GetElementValue(coreProperties.Root, Dc + "description");
        metadata.Language = GetElementValue(coreProperties.Root, Dc + "language");
        metadata.Identifier = GetElementValue(coreProperties.Root, Dc + "identifier");
        metadata.Keywords = GetElementValue(coreProperties.Root, Cp + "keywords");
        metadata.Category = GetElementValue(coreProperties.Root, Cp + "category");
        metadata.ContentStatus = GetElementValue(coreProperties.Root, Cp + "contentStatus");
        metadata.ContentType = GetElementValue(coreProperties.Root, Cp + "contentType");
        metadata.LastModifiedBy = GetElementValue(coreProperties.Root, Cp + "lastModifiedBy");
        metadata.RevisionNumber = GetElementValue(coreProperties.Root, Cp + "revision");
        metadata.Version = GetElementValue(coreProperties.Root, Cp + "version");
        metadata.CreatedAt = GetDateValue(coreProperties.Root, Dcterms + "created") ?? metadata.CreatedAt;
        metadata.UpdatedAt = GetDateValue(coreProperties.Root, Dcterms + "modified") ?? metadata.UpdatedAt;
        metadata.LastPrinted = GetDateValue(coreProperties.Root, Cp + "lastPrinted");
    }

    private static void FillAppMetadata(DocxMetadata metadata, XDocument? appProperties)
    {
        if (appProperties?.Root is null)
        {
            return;
        }

        metadata.ApplicationName = GetElementValue(appProperties.Root, Ep + "Application");
        metadata.PageCount = GetIntValue(appProperties.Root, Ep + "Pages");
        metadata.WordCount = GetIntValue(appProperties.Root, Ep + "Words");
        metadata.CharacterCount = GetIntValue(appProperties.Root, Ep + "Characters")
            ?? GetIntValue(appProperties.Root, Ep + "CharactersWithSpaces");
        metadata.Template = GetElementValue(appProperties.Root, Ep + "Template");
        metadata.Manager = GetElementValue(appProperties.Root, Ep + "Manager");
        metadata.Company = GetElementValue(appProperties.Root, Ep + "Company");
        metadata.ApplicationVersion = GetElementValue(appProperties.Root, Ep + "AppVersion");
        metadata.PresentationFormat = GetElementValue(appProperties.Root, Ep + "PresentationFormat");
        metadata.TotalEditingTime = GetElementValue(appProperties.Root, Ep + "TotalTime");
        metadata.LineCount = GetIntValue(appProperties.Root, Ep + "Lines");
        metadata.ParagraphCount = GetIntValue(appProperties.Root, Ep + "Paragraphs");
        metadata.SlideCount = GetIntValue(appProperties.Root, Ep + "Slides");
        metadata.NoteCount = GetIntValue(appProperties.Root, Ep + "Notes");
        metadata.HiddenSlideCount = GetIntValue(appProperties.Root, Ep + "HiddenSlides");
        metadata.MultimediaClipCount = GetIntValue(appProperties.Root, Ep + "MMClips");
        metadata.ScaleCrop = GetBoolValue(appProperties.Root, Ep + "ScaleCrop");
        metadata.SharedDocument = GetBoolValue(appProperties.Root, Ep + "SharedDoc");
        metadata.HyperlinksChanged = GetBoolValue(appProperties.Root, Ep + "HyperlinksChanged");
    }

    private static void FillCustomMetadata(DocxMetadata metadata, XDocument? customProperties)
    {
        if (customProperties?.Root is null)
        {
            return;
        }

        foreach (var property in customProperties.Root.Elements(Custom + "property"))
        {
            var name = (string?)property.Attribute("name");
            var value = property.Elements().FirstOrDefault()?.Value;
            if (!string.IsNullOrWhiteSpace(name) && value is not null)
            {
                metadata.CustomProperties[name] = value;
            }
        }
    }

    private static Dictionary<string, string> LoadRelationships(ZipArchive archive, string entryName)
    {
        var relationships = new Dictionary<string, string>(StringComparer.Ordinal);
        var xml = LoadXml(archive, entryName);
        if (xml?.Root is null)
        {
            return relationships;
        }

        foreach (var relationship in xml.Root.Elements(Rel + "Relationship"))
        {
            var id = (string?)relationship.Attribute("Id");
            var target = (string?)relationship.Attribute("Target");
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(target))
            {
                relationships[id] = target;
            }
        }

        return relationships;
    }

    private static XDocument? LoadXml(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName);
        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static string? GetElementValue(XElement root, XName name)
    {
        var value = root.Element(name)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static DateTime? GetDateValue(XElement root, XName name)
    {
        var value = GetElementValue(root, name);
        if (value is null)
        {
            return null;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
            ? date
            : null;
    }

    private static int? GetIntValue(XElement root, XName name)
    {
        var value = GetElementValue(root, name);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private static bool? GetBoolValue(XElement root, XName name)
    {
        var value = GetElementValue(root, name);
        if (value is null)
        {
            return null;
        }

        if (bool.TryParse(value, out var boolean))
        {
            return boolean;
        }

        return value switch
        {
            "0" => false,
            "1" => true,
            _ => null,
        };
    }

    private static int GetGridSpan(XElement? cellProperties)
    {
        var value = GetVal(cellProperties?.Element(W + "gridSpan"));
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var span) && span > 0
            ? span
            : 1;
    }

    private static void AddValue(Dictionary<string, string?> properties, string key, XElement? element)
    {
        var value = GetVal(element);
        if (!string.IsNullOrWhiteSpace(value))
        {
            properties[key] = value;
        }
    }

    private static void AddOnOffFormat(List<string> formats, XElement runProperties, string elementName, string format)
    {
        var element = runProperties.Element(W + elementName);
        if (element is not null && IsOn(element))
        {
            formats.Add(format);
        }
    }

    private static bool IsOn(XElement element)
    {
        var value = GetVal(element);
        return value is null
            || !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(value, "0", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(value, "off", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetVal(XElement? element)
    {
        return (string?)element?.Attribute(W + "val");
    }

    private static void RemoveCoveredMerges(Dictionary<int, CellDraft> verticalMerges, int columnIndex, int colSpan)
    {
        for (var col = columnIndex; col < columnIndex + colSpan; col++)
        {
            verticalMerges.Remove(col);
        }
    }

    private static void ResetStream(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }
    }

    private sealed class CellDraft(IParagraphBlock content, int colSpan)
    {
        public IParagraphBlock Content { get; } = content;

        public int RowSpan { get; set; } = 1;

        public int ColSpan { get; } = colSpan;
    }

    private sealed record DocxMetadata : BaseMetadata
    {
        public DocxMetadata(BaseMetadata metadata)
        {
            FileSize = metadata.FileSize;
            CreatedAt = metadata.CreatedAt;
            UpdatedAt = metadata.UpdatedAt;
            LastAccessedAt = metadata.LastAccessedAt;
            Author = metadata.Author;
            LastModifiedBy = metadata.LastModifiedBy;
            Title = metadata.Title;
            Subject = metadata.Subject;
            Keywords = metadata.Keywords;
            PageCount = metadata.PageCount;
            WordCount = metadata.WordCount;
            CharacterCount = metadata.CharacterCount;
            RevisionNumber = metadata.RevisionNumber;
            LastPrinted = metadata.LastPrinted;
            ApplicationName = metadata.ApplicationName;
            FileAttributes = metadata.FileAttributes;
        }

        public string? Description { get; set; }

        public string? Language { get; set; }

        public string? Identifier { get; set; }

        public string? Category { get; set; }

        public string? ContentStatus { get; set; }

        public string? ContentType { get; set; }

        public string? Version { get; set; }

        public string? Template { get; set; }

        public string? Manager { get; set; }

        public string? Company { get; set; }

        public string? ApplicationVersion { get; set; }

        public string? PresentationFormat { get; set; }

        public string? TotalEditingTime { get; set; }

        public int? LineCount { get; set; }

        public int? ParagraphCount { get; set; }

        public int? SlideCount { get; set; }

        public int? NoteCount { get; set; }

        public int? HiddenSlideCount { get; set; }

        public int? MultimediaClipCount { get; set; }

        public bool? ScaleCrop { get; set; }

        public bool? SharedDocument { get; set; }

        public bool? HyperlinksChanged { get; set; }

        public Dictionary<string, string?> CustomProperties { get; } = new(StringComparer.OrdinalIgnoreCase);

        public override Dictionary<string, string?> ToDictionary()
        {
            var dictionary = base.ToDictionary();
            dictionary.Remove(nameof(CustomProperties));

            foreach (var (key, value) in CustomProperties)
            {
                dictionary[$"{nameof(CustomProperties)}.{key}"] = value;
            }

            return dictionary;
        }
    }

    private sealed record DocxInlineLinkBlock(string TextValue) : InlineLinkBlock(TextValue);
}
