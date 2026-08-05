using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.DocParser.Block
{
    /// <summary>
    /// A list.
    /// </summary>
    public record ListBlock<T> : ParagraphBlock<IEnumerable<T>>
        where T: IParagraphBlock
    {
        public ListBlock(IEnumerable<T> items) : base(items)
        {
        }

        /// <summary>
        /// Type of list. E.g. ordered, unordered, quote, etc.
        /// </summary>
        public string ListType { get; init; } = string.Empty;
        /// <summary>
        /// Title of list.
        /// </summary>
        public string Title { get; init; } = string.Empty;
    }
}
