using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.DocParser.Block.Inline
{
    /// <summary>
    /// An inline formatted text.
    /// </summary>
    public record InlineFormattedBlock : InlineTextBlock
    {
        /// <summary>
        /// Format type, e.g. "formula", "code", etc.
        /// </summary>
        public string Formatting { get; init; } = string.Empty;

        public InlineFormattedBlock(string text) : base(text)
        {
        }
    }
}
