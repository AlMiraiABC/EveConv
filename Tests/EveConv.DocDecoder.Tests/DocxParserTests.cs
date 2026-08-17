using System.IO.Compression;
using EveConv.Abstraction;
using EveConv.Abstraction.DocParser.Block;
using EveConv.Abstraction.DocParser.Block.Inline;
using EveConv.DocDecoder.Parser;

namespace EveConv.DocDecoder.Tests;

public class DocxParserTests
{
    private const string DocxMimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    [Fact]
    public async Task ParseAsync_ExtractsParagraphsTablesAndMetadata()
    {
        using var stream = CreateDocxStream();
        var parser = new DocxParser(new TestMimeTypeDetection());

        var document = await parser.ParseAsync("sample.docx", stream, TestContext.Current.CancellationToken);

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

    [Theory]
    [InlineData("docx")]
    [InlineData(".docx")]
    [InlineData(DocxMimeType)]
    [InlineData(DocxMimeType + "; charset=utf-8")]
    public void Accept_ReturnsTrueForDocxTypes(string fileType)
    {
        var parser = new DocxParser(new TestMimeTypeDetection());

        Assert.True(parser.Accept(fileType));
    }

    private static MemoryStream CreateDocxStream()
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
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
                </Relationships>
                """);
            AddEntry(archive, "word/document.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
                            xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <w:body>
                    <w:p>
                      <w:r><w:t xml:space="preserve">Hello </w:t></w:r>
                      <w:r><w:rPr><w:b/></w:rPr><w:t>World</w:t></w:r>
                      <w:hyperlink r:id="rId1"><w:r><w:t xml:space="preserve"> Link</w:t></w:r></w:hyperlink>
                    </w:p>
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

        stream.Position = 0;
        return stream;
    }

    private static void AddEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
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
