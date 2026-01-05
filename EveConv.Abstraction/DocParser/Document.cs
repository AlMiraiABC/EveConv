using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.DocParser
{
    /// <summary>
    /// A document.
    /// </summary>
    public sealed class Document
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
        public IEnumerable<IParagraph> Paragraphs { get; init; } = [];

        /// <summary>
        /// The collection of sections in this document.
        /// </summary>
        /// <remarks>
        /// It may be empty if the document is not organized by sections.
        /// </remarks>
        public IEnumerable<Section> Sections { get; init; } = [];
    }

    /// <summary>
    /// A document capter or section.
    /// </summary>
    public sealed class Section
    {
        /// <summary>
        /// Section numbering.
        /// </summary>
        public string Numbering { get; init; }

        /// <summary>
        /// Section title.
        /// </summary>
        public string Title { get; init; }

        /// <summary>
        /// The collection of paragraphs contained in this section if it is not organized by sub-sections.
        /// </summary>
        /// <remarks>
        /// It could be empty if it contains only sub-sections.
        /// </remarks>
        public IEnumerable<IParagraph> Paragraphs { get; init; } = [];

        /// <summary>
        /// The collection of sub-sections contained in this section.
        /// </summary>
        /// <remarks>
        /// It is empty if there is no sub-section(a leaf node).
        /// </remarks>
        public IEnumerable<Section> SubSections { get; init; } = [];

        /// <summary>
        /// Create a section instance.
        /// </summary>
        /// <param name="numbering"><see cref="Numbering"/></param>
        /// <param name="title"><see cref="Title"/></param>
        public Section(string numbering, string title)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(numbering);
            ArgumentException.ThrowIfNullOrWhiteSpace(title);
            Numbering = numbering;
            Title = title;
        }
    }

}
