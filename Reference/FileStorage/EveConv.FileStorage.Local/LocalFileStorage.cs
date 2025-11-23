using System.Runtime.CompilerServices;
using EveConv.Abstraction;
using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.FileStorage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EveConv.FileStorage.Local
{
    /// <summary>
    /// A simple local file storage that stores files on local file system.
    /// </summary>
    public class LocalFileStorage : IFileStorage
    {
        private readonly string _rootPath;
        private readonly IMimeTypeDetection _mimeTypeDetection;
        private readonly ILogger<LocalFileStorage> _logger;

        public LocalFileStorage(IOptions<LocalFileStorageConfiguration> config, IMimeTypeDetection mimeTypeDetection, ILogger<LocalFileStorage>? logger)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentException.ThrowIfNullOrWhiteSpace(config.Value.RootPath);
            this._rootPath = config.Value.RootPath;
            this._mimeTypeDetection = mimeTypeDetection;
            this._logger = logger ?? DefaultLogger<LocalFileStorage>.Instance;
        }

        public async Task CreateIndexAsync(string indexName, CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(GetIndexPath(indexName));
            if(_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Created index '{idx}' at path '{path}'.", indexName, GetIndexPath(indexName));
            }
            return;
        }

        public async Task EnsureIndexExistsAsync(string indexName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
            if (Directory.Exists(GetIndexPath(indexName)))
            {
                return;
            }
            await CreateIndexAsync(indexName, cancellationToken);
        }

        public async Task DeleteFileAsync(string indexName, string fileId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileId);
            var fp = Path.Combine(GetIndexPath(indexName), fileId);
            if (!Directory.Exists(fp))
            {
                if(_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("File folder '{fid}' in index '{idx}' does not exist, no need to delete.", fileId, indexName);
                }
                return;
            }
            Directory.Delete(fp, true);
            if(_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Deleted file folder '{fid}' in index '{idx}'.", fileId, indexName);
            }
        }

        public async Task<StreamableFileContent> ReadFileAsync(string indexName, string fileId, string fileName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileId);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            var f = Path.Combine(GetIndexPath(indexName), fileId, fileName);
            if (!Directory.Exists(f))
            {
                throw new FileNotFoundException("File not found.", f);
            }
            var fi = new FileInfo(f);
            return new StreamableFileContent(
                fileName: fi.Name,
                fileSize: fi.Length,
                fileType: this._mimeTypeDetection.GetFileType(fileName),
                lastWriteTimeUtc: fi.LastWriteTimeUtc,
                asyncStreamDelegate: async () =>
                {
                    var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return fs;
                });
        }

        public async Task WriteFileAsync(string indexName, string fileId, string fileName, Stream fileContent, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileId);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            // File name may contains invalid characters or too long. Throw exception directly.
            // It should be validated by caller.
            // Do not modify file name, cannot return modified name to caller.
            var fp = await EnsureFileFolderAsync(fileId);
            var f = Path.Combine(fp, fileName);
            using var fs = new FileStream(f, FileMode.Create, FileAccess.Write, FileShare.None);
            await fileContent.CopyToAsync(fs, cancellationToken);
        }

        #region private methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GetIndexPath(string indexName)
        {
            return Path.Combine(this._rootPath, indexName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<string> EnsureFileFolderAsync(string fileId)
        {
            await EnsureIndexExistsAsync(fileId);
            var fp = Path.Combine(GetIndexPath(fileId), fileId);
            if (Directory.Exists(fp))
            {
                return fp;
            }
            Directory.CreateDirectory(fp);
            return fp;
        }

        #endregion
    }
}
