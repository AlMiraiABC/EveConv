using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Amazon.S3.Model;
using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.FileStorage;
using EveConv.S3Helper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EveConv.FileStorage.S3
{
    public class S3FileStorage : IFileStorage
    {
        private readonly S3Configuration _config;
        private readonly ILogger<S3FileStorage> _logger;

        private readonly S3ObjectHelper _client;

        public S3FileStorage(IOptions<S3Configuration> options, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(options);
            options.Value.Valid();
            this._config = options.Value;
            this._logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<S3FileStorage>();
            this._client = new(this._config, loggerFactory);
        }

        public async Task CreateIndexAsync(string indexName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
            await this._client.CreateBucketAsync(indexName, cancellationToken);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Created bucket {idx}", indexName);
            }
        }

        public async Task EnsureIndexExistsAsync(string indexName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
            await this._client.EnsureBucketExistsAsync(indexName, cancellationToken);
        }

        public async Task<StreamableFileContent> ReadFileAsync(string indexName, string fileId, string fileName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileId);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            var key = GetObjectKey(fileId, fileName);
            // HACK: How about return a temporary read-only file URL, that download by caller or user.
            try
            {
                var file = await _client.GetObjectAsync(indexName, key, cancellationToken);
                return new StreamableFileContent(
                    fileName: fileName,
                    fileSize: file.ContentLength,
                    fileType: file.Headers.ContentType,
                    lastWriteTimeUtc: file.LastModified ?? default,
                    asyncStreamDelegate: async () => file.ResponseStream);
            }
            catch (NoSuchKeyException)
            {
                throw new FileNotFoundException($"File not found in bucket {indexName}", key);
            }
        }

        public async Task DeleteFileAsync(string indexName, string fileId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileId);
            var (successed, failed) = await this._client.DeleteObjectsAsync(indexName, fileId, cancellationToken);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Deleted {del}/{all} objects in index '{idx}' with prefix '{fid}'",
                    successed.Count, successed.Count + failed.Count, indexName, fileId);
            }
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Deleted successed objects: {successed}", string.Join(", ", successed));
                _logger.LogTrace("Deleted failed objects: {failed}", string.Join(", ", failed));
            }
        }

        public async Task WriteFileAsync(string indexName, string fileId, string fileName, Stream fileContent, CancellationToken cancellationToken = default)
        {
            // multi part upload need split stream to chunk, that read all content from stream.
            ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileId);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            ArgumentNullException.ThrowIfNull(fileContent);
            await this._client.UploadObjectAsync(indexName, GetObjectKey(fileId, fileName), fileContent, cancellationToken);
        }

        #region private helper

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetObjectKey(string fileId, string fileName)
        {
            return $"{fileId}/{fileName}";
        }

        #endregion
    }
}
