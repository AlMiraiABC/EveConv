using System;
using System.Collections.Generic;
using System.Text;
using EveConv.Abstraction;
using EveConv.Abstraction.DocParser.Block;
using EveConv.Abstraction.Downloader;

namespace EveConv.DocDecoder.Parser
{
    public class TxtParser : DocParseable
    {
        public TxtParser(IMimeTypeDetection mimeTypeDetection, IDownloader downloader) : base(mimeTypeDetection, downloader)
        {
        }

        public override bool Accept(string fileType)
        {
            return true;
        }

        protected override async Task<(IEnumerable<IParagraphBlock> Paragraphs, IEnumerable<SectionBlock> Sections)> ParseAsync(StreamableFileContent file, CancellationToken cancellationToken = default)
        {
            using var stream = await file.GetStreamAsync();
            using var reader = new StreamReader(stream, leaveOpen: true);
            var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            var paragraphs = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(c => new PlainTextBlock(c))
                .ToList();
            return (paragraphs, Enumerable.Empty<SectionBlock>());
        }
    }
}
