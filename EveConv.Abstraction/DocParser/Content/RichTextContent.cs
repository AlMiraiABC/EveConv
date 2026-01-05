using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.DocParser.Content
{
    /// <summary>
    /// A rich text.
    /// </summary>
    public sealed record RichTextContent : Paragraph<IParagraph>
    {
        /// <summary>
        /// Optional raw data encoded to base64 string. E.g. images.
        /// </summary>
        public string? Base64Data { get; set; }

        /// <summary>
        /// Type of <see cref="Content"/>. E.g. html, markdown, diagram, code, formula, etc.
        /// </summary>
        public string ContentType { get; }

        /// <summary>
        /// Create a rich text content instance.
        /// </summary>
        /// <param name="content"><see cref="Content"/></param>
        /// <param name="contentType"><see cref="ContentType"/></param>
        public RichTextContent(IParagraph content, string contentType) : base(content)
        {
            ContentType = contentType;
        }
    }
}
