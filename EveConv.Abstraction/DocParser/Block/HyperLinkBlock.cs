using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.DocParser.Block
{

    /// <summary>
    /// A hyperlink.
    /// </summary>
    public sealed record HyperLinkBlock : ParagraphBlock<IParagraphBlock>
    {
        /// <summary>
        /// Uri of this link.
        /// </summary>
        public string Uri { get; init; } = string.Empty;

        /// <summary>
        /// Optional title of this link.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Create a hyperlink content instance.
        /// </summary>
        /// <param name="uri"><see cref="Uri"/></param>
        public HyperLinkBlock(IParagraphBlock content, string uri) : base(content)
        {
            Uri = uri;
        }
    }
}
