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
        /// Determines whether the specified file type is accepted by the current filter.
        /// </summary>
        /// <param name="fileType">The file extension or media type.</param>
        /// <returns><see langword="true"/> if the file type is accepted; otherwise, <see langword="false"/>.</returns>
        public bool Accept(string fileType);

        /// <summary>
        /// Asynchronously parses the specified file stream to document.
        /// </summary>
        /// <param name="fileStream">The specified file stream.</param>
        /// <param name="cancellationToken">Task cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation that contains a structured document.</returns>
        async Task<Document> ParseAsync(Stream fileStream, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            await fileStream.CopyToAsync(ms, cancellationToken);
            return await ParseAsync(ms.ToArray(), cancellationToken);
        }

        /// <summary>
        /// Asynchronously parses the specified file path(on local machine) to document.
        /// </summary>
        /// <param name="filePath">The specified file path.</param>
        /// <param name="cancellationToken">Task cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation that contains a structured document.</returns>
        /// <exception cref="FileNotFoundException">The specified file path is not exist.</exception>
        async Task<Document> ParseAsync(string filePath, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("File not found on local machine", filePath);
            }
            using var fs = File.OpenRead(filePath);
            return await ParseAsync(fs, cancellationToken);
        }

        /// <summary>
        /// Asynchronously parses the specified file URI to document.
        /// </summary>
        /// <param name="fileUri">The specified file URI.</param>
        /// <param name="cancellationToken">Task cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation that contains a structured document.</returns>
        Task<Document> ParseAsync(Uri fileUri, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously parses the specified file data to document.
        /// </summary>
        /// <param name="fileData">The specified file data that read to bytes.</param>
        /// <param name="cancellationToken">Task cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation that contains a structured document.</returns>
        Task<Document> ParseAsync(byte[] fileData, CancellationToken cancellationToken = default);
    }
}
