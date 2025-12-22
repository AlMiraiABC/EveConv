using System;
using System.Collections.Generic;
using System.Text;
using EveConv.Abstraction;
using EveConv.Abstraction.DocParser;

namespace EveConv.DocDecoder
{
    internal class TxtDecoder : DocDecodeable
    {
        public TxtDecoder(IMimeTypeDetection mimeTypeDetection) : base(mimeTypeDetection)
        {
        }

        public override bool Accept(string fileType)
        {
            return true;
        }

        protected override async Task<(IEnumerable<Paragraph> Paragraphs, IEnumerable<Section> Sections)> ParseAsync(Stream fileStream, CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(fileStream);
            var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            var paragraphs = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(c => new Paragraph(c))
                .ToList();
            return (paragraphs, []);
        }
    }
}
