using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.DocParser.Block.Inline
{
    /// <summary>
    /// An inline formated text.
    /// </summary>
    public record InlineFormatedBlock : InlineTextBlock
    {
        /// <summary>
        /// Format type, e.g. "bold", "italic", "code", etc.
        /// </summary>
        public string Formating { get; init; } = string.Empty;

        public InlineFormatedBlock(string text) : base(text)
        {
        }
    }
}
