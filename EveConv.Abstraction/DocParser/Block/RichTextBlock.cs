using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.DocParser.Block
{
    /// <summary>
    /// A rich text.
    /// </summary>
    public sealed record RichTextBlock : ParagraphBlock<IEnumerable<IParagraphBlock>>
    {
        /// <summary>
        /// Optional raw data encoded to base64 string. E.g. images.
        /// </summary>
        public string? Base64Data { get; init; }

        /// <summary>
        /// Type of <see cref="Content"/>. E.g. html, markdown, diagram, code, formula, etc.
        /// </summary>
        public string ContentType { get; init; }

        /// <summary>
        /// Create a rich text content instance.
        /// </summary>
        /// <param name="content"><see cref="Content"/></param>
        /// <param name="contentType"><see cref="ContentType"/></param>
        public RichTextBlock(IParagraphBlock content, string contentType) : base([content])
        {
            ContentType = contentType;
        }

        /// <summary>
        /// Create a rich text content instance.
        /// </summary>
        /// <param name="content"><see cref="Content"/></param>
        /// <param name="contentType"><see cref="ContentType"/></param>
        public RichTextBlock(IEnumerable<IParagraphBlock> content, string contentType) : base(content)
        {
            ContentType = contentType;
        }
    }
}
