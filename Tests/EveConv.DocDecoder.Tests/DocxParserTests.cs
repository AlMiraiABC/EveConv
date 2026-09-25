using System.IO.Compression;
using EveConv.Abstraction;
using EveConv.Abstraction.DocParser.Block;
using EveConv.Abstraction.DocParser.Block.Inline;
using EveConv.DocDecoder.Parser;
using EveConv.Downloader;

namespace EveConv.DocDecoder.Tests;

public class DocxParserTests
{
    private const string DocxMimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
  private static readonly byte[] ImageBytes = [137, 80, 78, 71, 13, 10, 26, 10];

    [Fact]
    public async Task ParseAsync_ExtractsParagraphsTablesAndMetadata()
    {
        CreateDocx("sample.docx");
        var mimeTypeDetection = new TestMimeTypeDetection();
        var parser = new DocxParser(mimeTypeDetection, new LocalDownloader(mimeTypeDetection));

        var document = await parser.ParseAsync("sample.docx", TestContext.Current.CancellationToken);

        Assert.Equal(DocxMimeType, document.DocType);
        Assert.Equal("Unit Test", document.Metadata["Author"]);
        Assert.Equal("DocxParser sample", document.Metadata["Title"]);
        Assert.Equal("Microsoft Word", document.Metadata["ApplicationName"]);
        Assert.Equal("2", document.Metadata["PageCount"]);
        Assert.Equal("Parser test document", document.Metadata["Description"]);
        Assert.Equal("Tests", document.Metadata["Category"]);
        Assert.Equal("EveConv", document.Metadata["Company"]);
        Assert.Equal("16.0000", document.Metadata["ApplicationVersion"]);
        Assert.Equal("Research", document.Metadata["CustomProperties.Department"]);

        var blocks = document.Paragraphs.ToList();
        Assert.Equal(2, blocks.Count);

        var paragraph = Assert.IsType<PlainTextBlock>(blocks[0]);
        Assert.Equal("Hello World Link", GetText(paragraph));

        var inlines = paragraph.Content.ToList();
        var formatted = Assert.IsType<InlineFormattedBlock>(inlines[1]);
        Assert.Equal("World", formatted.Text);
        Assert.Equal("bold", formatted.Formatting);

        var link = Assert.IsAssignableFrom<InlineLinkBlock>(inlines[2]);
        Assert.Equal("https://example.com", link.Uri);
        Assert.Equal(" Link", link.Text);

        var table = Assert.IsType<TableBlock>(blocks[1]);
        var cells = table.Data.ToArray();
        Assert.Equal(2, cells.GetLength(0));
        Assert.Equal(2, cells.GetLength(1));
        Assert.Equal("A", GetText(cells[0, 0]!.Content));
        Assert.Equal(2, cells[0, 0]!.RowSpan);
        Assert.Equal("D", GetText(cells[0, 1]!.Content));
        Assert.Equal(TableCellBlock.TableCellSpanSource.Up, cells[1, 0]!.SpanSource);
        Assert.Equal("B", GetText(cells[1, 1]!.Content));
    }

    [Fact]
    public async Task ParseAsync_EmbeddedImage_ReturnsDataUriInDocumentOrder()
    {
        CreateDocx("image.docx", includeImage: true);
        var mimeTypeDetection = new TestMimeTypeDetection();
        var parser = new DocxParser(mimeTypeDetection, new LocalDownloader(mimeTypeDetection));

        var document = await parser.ParseAsync("image.docx", TestContext.Current.CancellationToken);

        var paragraph = Assert.IsType<RichTextBlock>(document.Paragraphs.ElementAt(1));
        var content = paragraph.Content.ToList();
        Assert.Equal(3, content.Count);
        Assert.Equal("Before", GetText(content[0]));
        var image = Assert.IsType<RichTextBlock>(content[1]);
        Assert.Equal("image/png", image.ContentType);
        Assert.Equal($"data:image/png;base64,{Convert.ToBase64String(ImageBytes)}", image.Data);
        Assert.Equal("Picture 1", image.Properties["Name"]);
        Assert.Equal("Test image", image.Properties["AlternativeText"]);
        Assert.Equal("Image title", image.Properties["Title"]);
        Assert.Equal("word/media/image1.png", image.Properties["ResourcePath"]);
        Assert.Equal("914400", image.Properties["WidthEmu"]);
        Assert.Equal("457200", image.Properties["HeightEmu"]);
        Assert.Equal("45", image.Properties["RotationDegrees"]);
        Assert.Equal(bool.TrueString, image.Properties["FlipHorizontal"]);
        Assert.Equal("10", image.Properties["CropLeftPercent"]);
        Assert.Equal("After", GetText(content[2]));
        var trailingText = Assert.IsType<PlainTextBlock>(content[2]);
        Assert.Equal(0, Assert.Single(trailingText.Content).CharStart);
    }

    [Theory]
    [InlineData("docx")]
    [InlineData(".docx")]
    [InlineData(DocxMimeType)]
    [InlineData(DocxMimeType + "; charset=utf-8")]
    public void Accept_ReturnsTrueForDocxTypes(string fileType)
    {
        var mimeTypeDetection = new TestMimeTypeDetection();
        var parser = new DocxParser(mimeTypeDetection, new LocalDownloader(mimeTypeDetection));

        Assert.True(parser.Accept(fileType));
    }

  private static void CreateDocx(string filePath, bool includeImage = false)
    {
        using (var fileStream = File.Create(filePath))
        {
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                AddEntry(archive, "[Content_Types].xml", """
                        <?xml version="1.0" encoding="UTF-8"?>
                        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                          <Default Extension="xml" ContentType="application/xml"/>
                          <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
                          <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
                          <Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>
                          <Override PartName="/docProps/custom.xml" ContentType="application/vnd.openxmlformats-officedocument.custom-properties+xml"/>
                        </Types>
                        """);
                AddEntry(archive, "word/_rels/document.xml.rels", """
                        <?xml version="1.0" encoding="UTF-8"?>
                        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink" Target="https://example.com" TargetMode="External"/>
                          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/image1.png"/>
                        </Relationships>
                        """);
                var imageRun = includeImage
                    ? """
                          <w:r><w:t>Before</w:t></w:r>
                          <w:r>
                            <w:drawing>
                              <wp:inline xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing">
                                <wp:extent cx="914400" cy="457200"/>
                                <wp:docPr id="1" name="Picture 1" descr="Test image" title="Image title"/>
                                <a:graphic xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
                                  <a:graphicData>
                                    <a:blip r:embed="rId2"/>
                                    <a:srcRect l="10000"/>
                                    <a:xfrm rot="2700000" flipH="1"/>
                                  </a:graphicData>
                                </a:graphic>
                              </wp:inline>
                            </w:drawing>
                          </w:r>
                          <w:r><w:t>After</w:t></w:r>
                    """
                    : string.Empty;
                AddEntry(archive, "word/document.xml", $$"""
                        <?xml version="1.0" encoding="UTF-8"?>
                        <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
                                    xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                          <w:body>
                            <w:p>
                              <w:r><w:t xml:space="preserve">Hello </w:t></w:r>
                              <w:r><w:rPr><w:b/></w:rPr><w:t>World</w:t></w:r>
                              <w:hyperlink r:id="rId1"><w:r><w:t xml:space="preserve"> Link</w:t></w:r></w:hyperlink>
                            </w:p>
                            <w:p>{{imageRun}}</w:p>
                            <w:tbl>
                              <w:tr>
                                <w:tc>
                                  <w:tcPr><w:vMerge w:val="restart"/></w:tcPr>
                                  <w:p><w:r><w:t>A</w:t></w:r></w:p>
                                </w:tc>
                                <w:tc><w:p><w:r><w:t>D</w:t></w:r></w:p></w:tc>
                              </w:tr>
                              <w:tr>
                                <w:tc><w:tcPr><w:vMerge/></w:tcPr><w:p/></w:tc>
                                <w:tc><w:p><w:r><w:t>B</w:t></w:r></w:p></w:tc>
                              </w:tr>
                            </w:tbl>
                          </w:body>
                        </w:document>
                        """);
                      AddEntry(archive, "word/media/image1.png", ImageBytes);
                AddEntry(archive, "docProps/core.xml", """
                        <?xml version="1.0" encoding="UTF-8"?>
                        <cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties"
                                          xmlns:dc="http://purl.org/dc/elements/1.1/"
                                          xmlns:dcterms="http://purl.org/dc/terms/">
                          <dc:title>DocxParser sample</dc:title>
                          <dc:description>Parser test document</dc:description>
                          <dc:creator>Unit Test</dc:creator>
                          <cp:category>Tests</cp:category>
                          <cp:lastModifiedBy>Codex</cp:lastModifiedBy>
                          <dcterms:created>2026-08-05T00:00:00Z</dcterms:created>
                        </cp:coreProperties>
                        """);
                AddEntry(archive, "docProps/app.xml", """
                        <?xml version="1.0" encoding="UTF-8"?>
                        <Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties">
                          <Application>Microsoft Word</Application>
                          <Company>EveConv</Company>
                          <Pages>2</Pages>
                          <Words>42</Words>
                          <Characters>128</Characters>
                          <AppVersion>16.0000</AppVersion>
                        </Properties>
                        """);
                AddEntry(archive, "docProps/custom.xml", """
                        <?xml version="1.0" encoding="UTF-8"?>
                        <Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/custom-properties"
                                    xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
                          <property fmtid="{D5CDD505-2E9C-101B-9397-08002B2CF9AE}" pid="2" name="Department">
                            <vt:lpwstr>Research</vt:lpwstr>
                          </property>
                        </Properties>
                        """);
            }
        }
    }

    private static void AddEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    private static void AddEntry(ZipArchive archive, string name, byte[] content)
    {
        var entry = archive.CreateEntry(name);
        using var stream = entry.Open();
        stream.Write(content);
    }

    private static string GetText(IParagraphBlock block)
    {
        return block switch
        {
            PlainTextBlock plainText => string.Concat(plainText.Content
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
                ".docx" => DocxMimeType,
                _ => IMimeTypeDetection.OCTET_STREAM_MIME_TYPE,
            };
        }
    }
}
