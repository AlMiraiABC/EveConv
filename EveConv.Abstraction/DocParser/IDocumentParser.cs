using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.DocParser
{
    /// <summary>
    /// Extracts document content and information into a structured document model.
    /// </summary>
    /// <remarks>
    /// Implementations are intended for content and metadata extraction, such as text,
    /// tables, links, embedded resources, and document properties. This contract does not
    /// require lossless document structure restoration, page-layout reconstruction, or
    /// round-tripping the original document format.
    /// </remarks>
    public interface IDocumentParser
    {
        /// <summary>
        /// Priority of the document parser. Lower values indicate higher priority.
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// Determines whether the specified file type is accepted by the current filter.
        /// </summary>
        /// <param name="fileType">The file extension or media type.</param>
        /// <returns><see langword="true"/> if the file type is accepted; otherwise, <see langword="false"/>.</returns>
        public bool Accept(string fileType);

        /// <summary>
        /// Asynchronously extracts content and information from the specified file stream.
        /// </summary>
        /// <param name="source">Original file source.</param>
        /// <param name="cancellationToken">Task cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation and contains extracted document content and information.</returns>
        Task<Document> ParseAsync(string source, CancellationToken cancellationToken = default);
    }
}
