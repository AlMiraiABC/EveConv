using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.DocParser.Block.Inline
{
    /// <summary>
    /// An inline link.
    /// </summary>
    public record InlineLinkBlock : InlineTextBlock
    {
        public string Uri { get; init; } = string.Empty;

        protected InlineLinkBlock(string text = "") : base(text)
        {
        }
    }
}
