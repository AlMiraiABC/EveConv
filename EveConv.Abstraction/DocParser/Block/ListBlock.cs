using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.DocParser.Block
{
    /// <summary>
    /// A list.
    /// </summary>
    public record ListBlock<T> : ParagraphBlock<IEnumerable<T>>
    {
        public ListBlock(ParagraphBlock<IEnumerable<T>> items) : base(items)
        {
        }

        /// <summary>
        /// Type of list. E.g. ordered, unordered, etc.
        /// </summary>
        public string ListType { get; init; } = string.Empty;
        /// <summary>
        /// Title of list.
        /// </summary>
        public string Title { get; init; } = string.Empty;
    }
}
