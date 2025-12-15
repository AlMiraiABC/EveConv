using System;
using System.Collections.Generic;
using System.Text;
using Amazon.S3.Model;
using EveConv.Abstraction;
using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.Downloader;
using EveConv.S3Helper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EveConv.Downloader
{
    public class S3Downloader : IDownloader
    {
        private readonly ILogger<S3Downloader> _logger;
        private readonly S3ObjectHelper _client;

        public S3Downloader(IOptions<S3Configuration> options, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(options);
            this._logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<S3Downloader>();
            this._client = new(options.Value, loggerFactory);
        }

        public async Task<StreamableFileContent> DownloadAsync(string filePath, CancellationToken token = default)
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
                return new(
                    Path.GetFileName(key),
                    response.ContentLength,
                    async () => response.ResponseStream,
                    response.Headers.ContentType,
                    response.LastModified);
            }
            catch (Exception ex) when (ex is NoSuchBucketException or NoSuchKeyException
                || ex is Amazon.S3.AmazonS3Exception s3ex && (s3ex.StatusCode == System.Net.HttpStatusCode.NotFound))
            {
                throw new FileNotFoundException($"File not found in bucket {bucketName}", key);
            }
        }

        private static (string BucketName, string Key) ParsePath(string filePath)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath);
            filePath = filePath.Trim().Replace('\\', '/');
            if (!Uri.TryCreate(filePath, UriKind.Absolute, out var uri)
                || !uri.Scheme.Equals("s3", StringComparison.CurrentCultureIgnoreCase)
                || string.IsNullOrWhiteSpace(uri.Host)
                || string.IsNullOrWhiteSpace(uri.AbsolutePath?.TrimStart('/')))
            {
                throw new ArgumentException("Invalid S3 file path format. Except 's3://<bucketName>/<key>'", nameof(filePath));
            }
            return (uri.Host, uri.AbsolutePath[1..]);
        }
    }
}
