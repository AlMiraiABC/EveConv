using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.FileStorage
{
    public interface IFileStorage
    {
        /// <summary>
        /// Asynchronously creates an index container.
        /// </summary>
        /// <param name="indexName">Index name of container</param>
        /// <param name="cancellationToken">Task cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        /// <remarks>Index is root path for all files, such as bucket or root folder.</remarks>
        Task CreateIndexAsync(string indexName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously ensures an index container exists. Creates it if not exists.
        /// </summary>
        /// <param name="indexName">Index name of container</param>
        /// <param name="cancellationToken">Task cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task EnsureIndexExistsAsync(string indexName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously writes a file to the specified index.
        /// </summary>
        /// <param name="indexName">Index name of container</param>
        /// <param name="fileId">Unique file id to avoid duplicated file name.</param>
        /// <param name="fileName">File origin file name.</param>
        /// <param name="fileContent">File content.</param>
        /// <param name="cancellationToken">Task cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        /// <remarks>It will overwrites file content if exists.</remarks>
        Task WriteFileAsync(string indexName, string fileId, string fileName, Stream fileContent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously deletes a file from the specified index.
        /// </summary>
        /// <param name="indexName">Index name of container</param>
        /// <param name="fileId">Unique file id to avoid duplicated file name.</param>
        /// <param name="cancellationToken">Task cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        /// <remarks>No exception will be thrown if the file does not exist.</remarks>
        Task DeleteFileAsync(string indexName, string fileId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously reads a file from the specified index.
        /// </summary>
        /// <param name="indexName">Index name of container</param>
        /// <param name="fileId">Unique file id to avoid duplicated file name.</param>
        /// <param name="fileName">File origin file name.</param>
        /// <param name="cancellationToken">Task cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation that contains a streamable file content.</returns>
        /// <exception cref="FileNotFoundException">File not found.</exception>
        Task<StreamableFileContent> ReadFileAsync(string indexName, string fileId, string fileName, CancellationToken cancellationToken = default);
    }
}
