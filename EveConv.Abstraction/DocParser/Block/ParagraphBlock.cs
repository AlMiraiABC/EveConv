using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.DocParser.Block
{
    /// <summary>
    /// Empty interface to represents a paragraph.
    /// </summary>
    public interface IParagraphBlock
    {

    }

    /// <summary>
    /// Paragraph with content.
    /// </summary>
    /// <typeparam name="T">Type of <see cref="Content"/>.</typeparam>
    public abstract record ParagraphBlock<T> : DocumentRangeBlock, IParagraphBlock
    {
        /// <summary>
        /// Content.
        /// </summary>
        public T Content { get; protected set; }
        /// <summary>
        /// Content numbering. E.g. Figure-1, Table-2, etc.
        /// </summary>
        public string Numbering { get; init; } = string.Empty;
        /// <summary>
        /// Create a paragraph with specified content.
        /// </summary>
        /// <param name="content"></param>
        protected ParagraphBlock(T content)
        {
            Content = content;
        }
    }
}
