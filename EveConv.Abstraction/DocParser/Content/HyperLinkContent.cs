using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.DocParser.Content
{

    /// <summary>
    /// A hyper link.
    /// </summary>
    public sealed record HyperLinkContent : Paragraph<string>
    {
        /// <summary>
        /// URI of link.
        /// </summary>
        /// <remarks>Alias of <see cref="Paragraph.Content"/></remarks>
        public string Uri => Content ?? string.Empty;
        /// <summary>
        /// Optional title of this link.
        /// </summary>
        public string? Title { get; }

        /// <summary>
        /// Create a hyper link content instance.
        /// </summary>
        /// <param name="uri"><see cref="Uri"/></param>
        /// <param name="title"><see cref="Title"/></param>
        public HyperLinkContent(string uri, string? title = null) : base(uri)
        {
            Title = title;
        }
    }
}
