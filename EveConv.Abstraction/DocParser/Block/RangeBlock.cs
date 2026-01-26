using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.DocParser.Block
{
    /// <summary>
    /// Multiple lines content in block.
    /// </summary>
    public abstract record DocumentRangeBlock : IDocumentBlock
    {
        public int LineStart { get; init; }
        public int LineEnd { get => Math.Max(LineStart, field); init; }

        /// <summary>
        /// Range of Line number.
        /// </summary>
        public string Source => $"{LineStart}-{LineEnd}";
    }

}
