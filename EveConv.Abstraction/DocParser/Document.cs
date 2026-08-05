using System;
using System.Collections.Generic;
using System.Text;
using EveConv.Abstraction.DocParser.Block;

namespace EveConv.Abstraction.DocParser
{
    public interface IDocumentBlock
    {
        string Source { get; }
    }

    /// <summary>
    /// A document.
    /// </summary>
    public sealed class Document : IDocumentBlock
    {
        /// <summary>
        /// The document type. Could be file extension or media type.
        /// </summary>
        public string DocType { get; init; }
        /// <summary>
        /// The document origin source. Could be file path, URI or others.
        /// </summary>
        public string Source { get; init; }

        /// <summary>
        /// Create a document instance.
        /// </summary>
        /// <param name="docType"><see cref="DocType"/></param>
        /// <param name="source"><see cref="Source"/></param>
        public Document(string docType, string source)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(docType);
            ArgumentException.ThrowIfNullOrWhiteSpace(source);
            DocType = docType;
            Source = source;
        }

        /// <summary>
        /// The collection of paragraphs contained in this document if it is not organized by sections.
        /// </summary>
        /// <remarks>
        /// It may be empty if the document is fully organized by sections.
        /// </remarks>
        public IEnumerable<IParagraphBlock> Paragraphs { get; init; } = [];

        /// <summary>
        /// The collection of sections in this document.
        /// </summary>
        /// <remarks>
        /// It may be empty if the document is not organized by sections.
        /// </remarks>
        public IEnumerable<SectionBlock> Sections { get; init; } = [];

        /// <summary>
        /// The collection of metadata about this document.
        /// </summary>
        public Dictionary<string, string?> Metadata { get; init; } = new();
    }

}
