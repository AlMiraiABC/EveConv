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
        public IEnumerable<Paragraph> Paragraphs { get; init; } = [];

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
        public IEnumerable<Paragraph> Paragraphs { get; init; } = [];

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

    /// <summary>
    /// A paragraph content.
    /// </summary>
    public sealed record Paragraph
    {
        /// <summary>
        /// Textual content.
        /// </summary>
        public string? Text { get; }

        /// <summary>
        /// Media content.
        /// </summary>
        public Media? Media { get; }

        /// <summary>
        /// Paragraph type.
        /// </summary>
        public ParagraphType Type { get; }

        /// <summary>
        /// Create a paragraph instance from text.
        /// </summary>
        /// <param name="text"><see cref="Text"/></param>
        public Paragraph(string text)
        {
            Text = text ?? string.Empty;
            Type = ParagraphType.Text;
        }

        /// <summary>
        /// Create a paragraph instance from media.
        /// </summary>
        /// <param name="media"><see cref="Media"/></param>
        public Paragraph(Media media)
        {
            Media = media ?? throw new ArgumentNullException(nameof(media));
            Type = ParagraphType.Media;
        }

        /// <summary>
        /// Type of paragraph.
        /// </summary>
        public enum ParagraphType
        {
            /// <summary>
            /// The paragraph contains textual content to <see cref="Text"/>.
            /// </summary>
            Text,
            /// <summary>
            /// The paragraph contains media content to <see cref="Media"/>.
            /// </summary>
            Media
        }
    }

    /// <summary>
    /// A media content.
    /// </summary>
    /// <remarks>
    /// A media could be represented by local path/URI or binary data.
    /// </remarks>
    public sealed record Media
    {
        /// <summary>
        /// Local path or URI of this media.
        /// </summary>
        public string? Path { get; }
        /// <summary>
        /// Binary data of this media.
        /// </summary>
        public byte[]? Data { get; }
        /// <summary>
        /// Media type, e.g. image/png, video/mp4, application/json
        /// </summary>
        public string MediaType { get; }

        /// <summary>
        /// Create a media instance from path or URI.
        /// </summary>
        /// <param name="path"><see cref="Path"/></param>
        /// <param name="type"><see cref="MediaType"/></param>
        public Media(string path, string type)
        {
            this.Path = path;
            this.MediaType = type;
        }

        /// <summary>
        /// Create a media instance from binary data.
        /// </summary>
        /// <param name="data"><see cref="Data"/></param>
        /// <param name="type"><see cref="MediaType"/></param>
        public Media(byte[] data, string type)
        {
            this.Data = data;
            this.MediaType = type;
        }

        /// <summary>
        /// Optional meida description.
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// Optional media name.
        /// </summary>
        public string? Name { get; init; }
    }
}
