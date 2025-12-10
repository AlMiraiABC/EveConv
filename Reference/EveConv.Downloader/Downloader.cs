using System;
using System.Collections.Generic;
using System.Text;
using EveConv.Abstraction;
using EveConv.Abstraction.Diagnostic;
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

        public Task<Stream> DownloadAsync(string filePath, CancellationToken token = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath);
            if (filePath.StartsWith("s3:"))
            {
                if (!this._downloaders.TryGetValue(typeof(S3Downloader), out var downloader))
                {
                    throw new InvalidOperationException("S3 downloader is not configured.");
                }
                var s3Path = filePath.Substring(3);
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Downloading file from S3 path '{s3Path}'", s3Path);
                }
                return downloader.DownloadAsync(s3Path, token);
            }
            throw new NotSupportedException($"Unsupported file path scheme in '{filePath}'.");
        }
    }
}
