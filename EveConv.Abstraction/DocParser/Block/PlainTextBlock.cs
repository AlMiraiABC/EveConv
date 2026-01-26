using System;
using System.Collections.Generic;
using System.Text;
using EveConv.Abstraction.DocParser.Block.Inline;

namespace EveConv.Abstraction.DocParser.Block
{

    /// <summary>
    /// A plain text.
    /// </summary>
    public sealed record PlainTextBlock : ParagraphBlock<IEnumerable<DocumentInlineBlock>>
    {
        /// <summary>
        /// Create a plain text content instance from text.
        /// </summary>
        /// <param name="content"><see cref="Content"/></param>
        public PlainTextBlock(string content) : base([new InlineTextBlock(content)])
        {
        }

        /// <summary>
        /// Create a plain text content instance from inline formated text.
        /// </summary>
        /// <param name="content"><see cref="Content"/></param>
        /// <remarks>These inline contents should be in the same line.</remarks>
        public PlainTextBlock(IEnumerable<DocumentInlineBlock> content) : base(content)
        {
        }

        public bool Equals(PlainTextBlock? other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            if (other is null)
            {
                return false;
            }
            return LineStart == other.LineStart
                && LineEnd == other.LineEnd
                && string.Equals(Numbering, other.Numbering, StringComparison.Ordinal)
                && Content.SequenceEqual(other.Content);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(LineStart);
            hash.Add(LineEnd);
            hash.Add(Numbering, StringComparer.Ordinal);
            foreach (var inline in Content)
            {
                hash.Add(inline);
            }
            return hash.ToHashCode();
        }

        public static explicit operator PlainTextBlock(string content) => new(content);
    }
}
