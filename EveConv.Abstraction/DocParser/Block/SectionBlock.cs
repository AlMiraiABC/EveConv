using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.DocParser.Block
{

    /// <summary>
    /// A document capter or section.
    /// </summary>
    public sealed record SectionBlock : DocumentRangeBlock
    {

        /// <summary>
        /// Section numbering.
        /// </summary>
        public string Numbering { get; init; }

        /// <summary>
        /// Section title.
        /// </summary>
        public string Title { get; init; }

        /// <summary>
        /// The collection of paragraphs contained in this section if it is not organized by sub-sections.
        /// </summary>
        /// <remarks>
        /// It could be empty if it contains only sub-sections.
        /// </remarks>
        public IEnumerable<IParagraphBlock> Paragraphs { get; init; } = [];

        /// <summary>
        /// The collection of sub-sections contained in this section.
        /// </summary>
        /// <remarks>
        /// It is empty if there is no sub-section(a leaf node).
        /// </remarks>
        public IEnumerable<SectionBlock> SubSections { get; init; } = [];

        /// <summary>
        /// Create a section instance.
        /// </summary>
        /// <param name="numbering"><see cref="Numbering"/></param>
        /// <param name="title"><see cref="Title"/></param>
        public SectionBlock(string numbering, string title)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(numbering);
            ArgumentException.ThrowIfNullOrWhiteSpace(title);
            Numbering = numbering;
            Title = title;
        }
    }
}
