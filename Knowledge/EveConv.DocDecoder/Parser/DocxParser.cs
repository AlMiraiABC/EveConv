using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using EveConv.Abstraction;
using EveConv.Abstraction.DocParser.Block;
using EveConv.Abstraction.DocParser.Block.Inline;
using EveConv.Abstraction.Downloader;

namespace EveConv.DocDecoder.Parser;

/// <summary>
/// Extracts structured content and metadata from Office Open XML DOCX documents.
/// </summary>
/// <remarks>
/// <para>
/// The parser extracts paragraph text, common run formatting, hyperlinks, numbering references,
/// tables (including horizontal and vertical cell spans), core and application metadata, and
/// custom document properties.
/// </para>
/// <para>
/// Embedded images referenced from <c>word/media</c> are emitted as <see cref="RichTextBlock"/>
/// instances. Their <see cref="RichTextBlock.Data"/> value is a data URI, their content type is
/// the detected image MIME type, and compact presentation metadata such as alternative text,
/// dimensions, rotation, flipping, and cropping is stored in the block properties. Text and
/// images retain their order within a paragraph. Images larger than 20 MiB are ignored.
/// </para>
/// <para>
/// This parser does not reproduce page layout or provide lossless OOXML round-tripping. It does
/// not interpret stylesheets, headers, footers, comments, footnotes, tracked changes, equations,
/// charts, SmartArt, embedded objects, macros, external images, ink strokes, DrawingML shapes,
/// VML shapes, or drawing canvases. Text or fallback images nested in unsupported objects may be
/// extracted when represented by otherwise supported WordprocessingML or image elements, but the
/// containing object's geometry, relationships, and visual semantics are not preserved.
/// </para>
/// </remarks>
public class DocxParser : DocParseable
{
    private const string DocxMimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const long MaxEmbeddedImageBytes = 20 * 1024 * 1024;

    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private static readonly XNamespace Cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Dcterms = "http://purl.org/dc/terms/";
    private static readonly XNamespace Ep = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";
    private static readonly XNamespace Custom = "http://schemas.openxmlformats.org/officeDocument/2006/custom-properties";

    /// <summary>
    /// Initializes a DOCX parser.
    /// </summary>
    /// <param name="mimeTypeDetection">The service used to identify the source document type.</param>
    /// <param name="downloader">The service used to retrieve the source document.</param>
    public DocxParser(IMimeTypeDetection mimeTypeDetection, IDownloader downloader) : base(mimeTypeDetection, downloader)
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

    protected override async Task<(IEnumerable<IParagraphBlock> Paragraphs, IEnumerable<SectionBlock> Sections)> ParseAsync(
        StreamableFileContent file,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = await file.GetStreamAsync();

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var document = LoadXml(archive, "word/document.xml");
        if (document?.Root is null)
        {
            return ([], []);
        }

        var relationships = LoadRelationships(archive, "word/_rels/document.xml.rels");
        var paragraphs = new List<IParagraphBlock>();
        var lineNumber = 1;

        foreach (var element in document.Root.Descendants(W + "body").Elements())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element.Name == W + "p")
            {
                var paragraph = ParseParagraph(element, relationships, archive, lineNumber);
                if (!IsEmpty(paragraph))
                {
                    paragraphs.Add(paragraph);
                }
                lineNumber++;
            }
            else if (element.Name == W + "tbl")
            {
                var table = ParseTable(element, relationships, archive, ref lineNumber);
                if (table is not null)
                {
                    paragraphs.Add(table);
                }
            }
        }

        return (paragraphs, []);
    }

    protected override async Task<Dictionary<string, string?>> GetMetadataAsync(
        string source,
        StreamableFileContent file,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var baseMetadata = await GetFileSystemMetadataAsync(source, file).ConfigureAwait(false);
        var metadata = new DocxMetadata(baseMetadata);
        using var stream = await file.GetStreamAsync();

        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            FillCoreMetadata(metadata, LoadXml(archive, "docProps/core.xml"));
            FillAppMetadata(metadata, LoadXml(archive, "docProps/app.xml"));
            FillCustomMetadata(metadata, LoadXml(archive, "docProps/custom.xml"));
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or IOException)
        {
        }

        return metadata.ToDictionary();
    }

    private static IParagraphBlock ParseParagraph(
        XElement paragraphElement,
        IReadOnlyDictionary<string, string> relationships,
        ZipArchive archive,
        int lineNumber)
    {
        var blocks = new List<IParagraphBlock>();
        var inlines = new List<DocumentInlineBlock>();
        var charIndex = 0;

        foreach (var child in paragraphElement.Elements())
        {
            ParseInlineContent(child, relationships, archive, blocks, inlines, ref charIndex);
        }

        var properties = GetParagraphProperties(paragraphElement);
        FlushTextBlock(blocks, inlines, lineNumber);
        if (blocks.Count == 1 && blocks[0] is PlainTextBlock plainText)
        {
            return plainText with
            {
                Numbering = GetNumbering(properties),
                Properties = properties,
            };
        }

        return new RichTextBlock(blocks, "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
        {
            LineStart = lineNumber,
            LineEnd = lineNumber,
            Numbering = GetNumbering(properties),
            Properties = properties,
        };
    }

    private static void ParseInlineContent(
        XElement element,
        IReadOnlyDictionary<string, string> relationships,
        ZipArchive archive,
        List<IParagraphBlock> blocks,
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
                ParseInlineContent(child, relationships, archive, blocks, inlines, ref charIndex, uri ?? hyperlinkUri);
            }

            return;
        }

        if (element.Name == W + "r")
        {
            ParseRun(element, relationships, archive, blocks, inlines, hyperlinkUri, ref charIndex);
            return;
        }

        foreach (var child in element.Elements())
        {
            ParseInlineContent(child, relationships, archive, blocks, inlines, ref charIndex, hyperlinkUri);
        }
    }

    private static void ParseRun(
        XElement run,
        IReadOnlyDictionary<string, string> relationships,
        ZipArchive archive,
        List<IParagraphBlock> blocks,
        List<DocumentInlineBlock> inlines,
        string? hyperlinkUri,
        ref int charIndex)
    {
        var formatting = GetRunFormatting(run.Element(W + "rPr"));

        foreach (var child in run.Elements())
        {
            if (child.Name == W + "drawing")
            {
                var image = ParseImage(child, relationships, archive);
                if (image is not null)
                {
                    FlushTextBlock(blocks, inlines);
                    blocks.Add(image);
                    charIndex = 0;
                }
                continue;
            }

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

    private static RichTextBlock? ParseImage(
        XElement drawing,
        IReadOnlyDictionary<string, string> relationships,
        ZipArchive archive)
    {
        var blip = drawing.Descendants(A + "blip").FirstOrDefault();
        var relationshipId = (string?)blip?.Attribute(R + "embed");
        if (relationshipId is null || !relationships.TryGetValue(relationshipId, out var target))
        {
            return null;
        }

        var entryPath = ResolveWordPartPath(target);
        if (!entryPath.StartsWith("word/media/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var entry = archive.GetEntry(entryPath);
        if (entry is null || entry.Length > MaxEmbeddedImageBytes)
        {
            return null;
        }

        using var imageStream = entry.Open();
        using var buffer = new MemoryStream((int)entry.Length);
        imageStream.CopyTo(buffer);

        var contentType = GetImageContentType(entryPath);

        return new RichTextBlock([], contentType)
        {
            Data = $"data:{contentType};base64,{Convert.ToBase64String(buffer.GetBuffer(), 0, (int)buffer.Length)}",
            Properties = GetImageProperties(drawing, entryPath),
        };
    }

    private static Dictionary<string, string?> GetImageProperties(XElement drawing, string entryPath)
    {
        var properties = new Dictionary<string, string?>
        {
            ["ResourcePath"] = entryPath,
        };

        var documentProperties = drawing.Descendants(Wp + "docPr").FirstOrDefault();
        AddAttribute(properties, "Name", documentProperties, "name");
        AddAttribute(properties, "AlternativeText", documentProperties, "descr");
        AddAttribute(properties, "Title", documentProperties, "title");

        var extent = drawing.Descendants(Wp + "extent").FirstOrDefault();
        AddAttribute(properties, "WidthEmu", extent, "cx");
        AddAttribute(properties, "HeightEmu", extent, "cy");

        var transform = drawing.Descendants(A + "xfrm").FirstOrDefault();
        var rotation = (string?)transform?.Attribute("rot");
        if (long.TryParse(rotation, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rotationUnits))
        {
            properties["RotationDegrees"] = (rotationUnits / 60000d).ToString(CultureInfo.InvariantCulture);
        }
        AddTrueAttribute(properties, "FlipHorizontal", transform, "flipH");
        AddTrueAttribute(properties, "FlipVertical", transform, "flipV");

        var sourceRectangle = drawing.Descendants(A + "srcRect").FirstOrDefault();
        AddPercentageAttribute(properties, "CropLeftPercent", sourceRectangle, "l");
        AddPercentageAttribute(properties, "CropTopPercent", sourceRectangle, "t");
        AddPercentageAttribute(properties, "CropRightPercent", sourceRectangle, "r");
        AddPercentageAttribute(properties, "CropBottomPercent", sourceRectangle, "b");
        return properties;
    }

    private static void AddAttribute(
        Dictionary<string, string?> properties,
        string key,
        XElement? element,
        XName attributeName)
    {
        var value = (string?)element?.Attribute(attributeName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            properties[key] = value;
        }
    }

    private static void AddTrueAttribute(
        Dictionary<string, string?> properties,
        string key,
        XElement? element,
        XName attributeName)
    {
        var value = (string?)element?.Attribute(attributeName);
        if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            properties[key] = bool.TrueString;
        }
    }

    private static void AddPercentageAttribute(
        Dictionary<string, string?> properties,
        string key,
        XElement? element,
        XName attributeName)
    {
        var value = (string?)element?.Attribute(attributeName);
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var percentageUnits))
        {
            properties[key] = (percentageUnits / 1000d).ToString(CultureInfo.InvariantCulture);
        }
    }

    private static void FlushTextBlock(
        List<IParagraphBlock> blocks,
        List<DocumentInlineBlock> inlines,
        int? lineNumber = null)
    {
        if (inlines.Count == 0)
        {
            return;
        }

        blocks.Add(new PlainTextBlock(inlines.ToArray())
        {
            LineStart = lineNumber ?? 0,
            LineEnd = lineNumber ?? 0,
        });
        inlines.Clear();
    }

    private static string ResolveWordPartPath(string target)
    {
        var path = target.Replace('\\', '/').TrimStart('/');
        var segments = new List<string>();
        foreach (var segment in ("word/" + path).Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
            {
                if (segments.Count > 0)
                {
                    segments.RemoveAt(segments.Count - 1);
                }
            }
            else if (segment != ".")
            {
                segments.Add(segment);
            }
        }
        return string.Join('/', segments);
    }

    private static string GetImageContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            ".jpeg" or ".jpg" => "image/jpeg",
            ".png" => "image/png",
            ".svg" => "image/svg+xml",
            ".tif" or ".tiff" => "image/tiff",
            ".webp" => "image/webp",
            _ => "application/octet-stream",
        };
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
        ZipArchive archive,
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

                    var content = ParseCellContent(cellElement, relationships, archive);
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
        IReadOnlyDictionary<string, string> relationships,
        ZipArchive archive)
    {
        var paragraphs = cellElement
            .Elements(W + "p")
            .Select((paragraph, index) => ParseParagraph(paragraph, relationships, archive, index + 1))
            .Where(paragraph => !IsEmpty(paragraph))
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

    private static bool IsEmpty(IParagraphBlock paragraph)
    {
        return paragraph switch
        {
            PlainTextBlock plainText => plainText.Content.All(inline => inline is not InlineTextBlock textBlock
                || string.IsNullOrWhiteSpace(textBlock.Text)),
            RichTextBlock richText => string.IsNullOrWhiteSpace(richText.Data)
                && richText.Content.All(IsEmpty),
            _ => false,
        };
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
