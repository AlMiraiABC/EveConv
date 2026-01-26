using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.DocParser.Block
{

    /// <summary>
    /// A hyper link.
    /// </summary>
    public sealed record HyperLinkBlock : ParagraphBlock<IParagraphBlock>
    {
        /// <summary>
        /// Optional title of this link.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Create a hyper link content instance.
        /// </summary>
        /// <param name="uri"><see cref="Uri"/></param>
        /// <param name="title"><see cref="Title"/></param>
        public HyperLinkBlock(IParagraphBlock content) : base(content)
        {
        }
    }
}
