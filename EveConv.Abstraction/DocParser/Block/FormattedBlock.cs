using System;
using System.Collections.Generic;
using System.Text;
using EveConv.Abstraction.DocParser.Block.Inline;

namespace EveConv.Abstraction.DocParser.Block;

public sealed record FormattedBlock : ParagraphBlock<IEnumerable<DocumentInlineBlock>>
{
    /// <summary>
    /// Format type, e.g. formula, code, quote, etc.
    /// </summary>
    public string Formatting { get; init; } = string.Empty;

    /// <summary>
    /// Create a plain text content instance from text.
    /// </summary>
    /// <param name="content"><see cref="Content"/></param>
    public FormattedBlock(string content) : base([new InlineTextBlock(content)])
    {
    }

    /// <summary>
    /// Create a plain text content instance from inline formated text.
    /// </summary>
    /// <param name="content"><see cref="Content"/></param>
    /// <remarks>These inline contents should be in the same line.</remarks>
    public FormattedBlock(IEnumerable<DocumentInlineBlock> content) : base(content)
    {
    }


    public bool Equals(FormattedBlock? other)
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
            && Content.SequenceEqual(other.Content)
            && string.Equals(Formatting, other.Formatting, StringComparison.Ordinal);
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
        hash.Add(Formatting, StringComparer.Ordinal);
        return hash.ToHashCode();
    }

    public static explicit operator FormattedBlock(string content) => new(content);
}
