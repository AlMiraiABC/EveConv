using System;
using System.Collections.Generic;
using System.Text;
using Amazon.S3.Model;
using EveConv.Abstraction;
using EveConv.Abstraction.Diagnostic;
using EveConv.S3Helper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EveConv.Downloader
{
    public class S3Downloader : IDownloader
    {
        private readonly ILogger<S3Downloader> _logger;
        private readonly S3ObjectHelper _client;

        public S3Downloader(IOptions<S3Configuration> options, ILoggerFactory? loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(options);
            this._logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<S3Downloader>();
            this._client = new(options.Value, loggerFactory);
        }

        public async Task<Stream> DownloadAsync(string filePath, CancellationToken token = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath);
            var (bucketName, key) = ParsePath(filePath);
            try
            {
                var response = await this._client.GetObjectAsync(bucketName, key, token);
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    this._logger.LogDebug("Got object {key} in bucket {bucket} with size {size}", key, bucketName, response.ContentLength);
                }
                return response.ResponseStream;
            }
            catch (NoSuchKeyException)
            {
                throw new FileNotFoundException($"File not found in bucket {bucketName}", key);
            }
        }

        private (string BucketName, string Key) ParsePath(string filePath)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath);
            filePath = filePath.Replace('\\', '/').TrimStart('/');
            var parts = filePath.Split('/', 2);
            if (parts.Length == 1)
            {
                throw new ArgumentException("Invalid S3 file path format. Except '<bucketName>/<key>'.", nameof(filePath));
            }
            return (parts[0], parts[1]);
        }
    }
}
