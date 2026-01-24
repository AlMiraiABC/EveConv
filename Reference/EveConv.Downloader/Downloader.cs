using System;
using System.Collections.Generic;
using System.Text;
using EveConv.Abstraction;
using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.Downloader;
using Microsoft.Extensions.Logging;

namespace EveConv.Downloader
{
    public class Downloader : IDownloader
    {
        private readonly ILogger _logger;

        private readonly Dictionary<Type, IDownloader> _downloaders;

        public Downloader(IEnumerable<IDownloader> downloaders, ILoggerFactory? loggerFactory)
        {
            this._logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<Downloader>();
            this._downloaders = downloaders.ToDictionary(i => i.GetType(), i => i);
        }

        public async Task<StreamableFileContent> DownloadAsync(string filePath, CancellationToken token = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath);
            if (filePath.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                || filePath.StartsWith('.')
                || filePath.StartsWith('/')
                || filePath.StartsWith('\\'))
            {
                var downloader = GetDownloader<LocalDownloader>("file");
                var localPath = filePath;
                if (localPath.StartsWith("file:"))
                {
                    localPath = filePath[5..];
                }
                return await DownloadAsync(downloader, localPath, "file", token);
            }
            if (filePath.StartsWith("s3:", StringComparison.OrdinalIgnoreCase))
            {
                var downloader = GetDownloader<S3Downloader>("s3");
                var s3Path = filePath[3..];
                return await DownloadAsync(downloader, s3Path, "s3", token);
            }
            if (filePath.StartsWith("http:") || filePath.StartsWith("https:"))
            {
                var downloader = GetDownloader<HttpDownloader>("http");
                return await DownloadAsync(downloader, filePath, "http", token);
            }
            throw new NotSupportedException($"Unsupported file path scheme in '{filePath}'.");

            IDownloader GetDownloader<T>(string prefix)
            {
                if (!this._downloaders.ContainsKey(typeof(T)))
                {
                    throw new InvalidOperationException($"{prefix} downloader is not configured.");
                }
                return this._downloaders[typeof(T)];
            }
        }

        private async Task<StreamableFileContent> DownloadAsync(IDownloader downloader, string filePath, string prefix, CancellationToken token = default)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Downloading file from {prefix} path '{path}'", prefix, filePath);
            }
            return await downloader.DownloadAsync(filePath, token);
        }
    }
}
