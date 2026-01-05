using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.DocParser.Content
{

    /// <summary>
    /// A plain text.
    /// </summary>
    public sealed record PlainTextContent : Paragraph<string>
    {
        /// <summary>
        /// Create a plain text content instance.
        /// </summary>
        /// <param name="content"><see cref="Content"/></param>
        public PlainTextContent(string content) : base(content)
        {
        }

        public static implicit operator string(PlainTextContent plainText) => plainText.Content ?? string.Empty;
        public static explicit operator PlainTextContent(string content) => new(content);
    }
}
