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
        /// <summary>
        /// Content numbering. E.g. Figure-1, Table-2, etc.
        /// </summary>
        string Numbering { get; }

        /// <summary>
        /// Extended properties or information.
        /// </summary>
        Dictionary<string, string?> Properties { get; }
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

        public string Numbering { get; init; } = string.Empty;

        public Dictionary<string, string?> Properties { get; init; } = [];

        /// <summary>
        /// Create a paragraph with specified content.
        /// </summary>
        /// <param name="content"></param>
        protected ParagraphBlock(T content)
        {
            Content = content;
        }


        /// <inheritdoc/>
        /// <remarks>
        /// Ignore reference type equality such as <see cref="Dictionary{TKey, TValue}"/>.
        /// </remarks>
        public virtual bool Equals(ParagraphBlock<T>? other)
        {
            return ReferenceEquals(this, other)
                || (other is not null
                    && base.Equals(other)
                    && EqualityComparer<T>.Default.Equals(Content, other.Content)
                    && string.Equals(Numbering, other.Numbering, StringComparison.Ordinal));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Ignore reference type hashcode such as <see cref="Dictionary{TKey, TValue}"/>.
        /// </remarks>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(base.GetHashCode());
            hash.Add(Content);
            hash.Add(Numbering, StringComparer.Ordinal);
            return hash.ToHashCode();
        }
    }
}
