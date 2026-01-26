using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.DocParser.Block.Inline
{
    /// <summary>
    /// An inline content block in document.
    /// </summary>
    public abstract record DocumentInlineBlock : IDocumentBlock
    {
        /// <summary>
        /// The first char position.
        /// </summary>
        public int CharStart { get; init; }
        /// <summary>
        /// The last char position.
        /// </summary>
        /// <remarks>Defaults to <see cref="CharStart"/>.</remarks>
        public int CharEnd { get => Math.Max(CharStart, field); init; }

        /// <summary>
        /// Range of character position.
        /// </summary>
        public string Source => $"{CharStart}-{CharEnd}";
    }

    /// <summary>
    /// An inline plain text.
    /// </summary>
    public record InlineTextBlock : DocumentInlineBlock
    {
        public string Text { get; init; } = string.Empty;

        public InlineTextBlock(string text)
        {
            Text = text;
        }

        public static explicit operator InlineTextBlock(string text) => new(text);
    }
}
