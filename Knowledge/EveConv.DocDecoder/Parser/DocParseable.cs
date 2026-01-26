using System;
using System.Collections.Generic;
using System.Text;
using EveConv.Abstraction;
using EveConv.Abstraction.DocParser;
using EveConv.Abstraction.DocParser.Block;

namespace EveConv.DocDecoder.Parser
{
    internal abstract class DocParseable : IDocumentParser
    {
        public const int DEFAULT_PRIORITY = 100;

        protected IMimeTypeDetection _mimeTypeDetection;

        protected DocParseable(IMimeTypeDetection mimeTypeDetection)
        {
            this._mimeTypeDetection = mimeTypeDetection;
        }

        public virtual int Priority => DEFAULT_PRIORITY;

        public abstract bool Accept(string fileType);

        public async Task<Document> ParseAsync(string source, Stream fileStream, CancellationToken cancellationToken = default)
        {
            var (paragraphs, sections) = await ParseAsync(fileStream, cancellationToken).ConfigureAwait(false);
            return new(GetFileType(source), source)
            {
                Paragraphs = paragraphs ?? [],
                Sections = sections ?? [],
            };
        }

        protected virtual string GetFileType(string source)
        {
            return _mimeTypeDetection.GetFileType(source);
        }

        protected virtual Task<(IEnumerable<IParagraphBlock> Paragraphs, IEnumerable<SectionBlock> Sections)> ParseAsync(Stream fileStream, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<(IEnumerable<IParagraphBlock>, IEnumerable<SectionBlock>)>(([], []));
        }
    }
}
