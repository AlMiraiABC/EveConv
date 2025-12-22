using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.DocParser
{
    /// <summary>
    /// 
    /// </summary>
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
        /// Asynchronously parses the specified file stream to document.
        /// </summary>
        /// <param name="source">Original file source.</param>
        /// <param name="fileStream">The specified file stream.</param>
        /// <param name="cancellationToken">Task cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation that contains a structured document.</returns>
        Task<Document> ParseAsync(string source, Stream fileStream, CancellationToken cancellationToken = default);
    }
}
