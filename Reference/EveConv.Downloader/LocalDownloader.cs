using System;
using System.Collections.Generic;
using System.Text;
using EveConv.Abstraction;
using EveConv.Abstraction.Downloader;

namespace EveConv.Downloader
{
    /// <summary>
    /// Download file from local file system.
    /// </summary>
    public class LocalDownloader : IDownloader
    {
        private readonly IMimeTypeDetection _mimeTypeDetection;
        public LocalDownloader(IMimeTypeDetection mimeTypeDetection)
        {
            _mimeTypeDetection = mimeTypeDetection;
        }

        public Task<StreamableFileContent> DownloadAsync(string filePath, CancellationToken token = default)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("File not found", filePath);
            }
            var fileInfo = new FileInfo(filePath);
            var streamableFileContent = new StreamableFileContent(
                fileName: fileInfo.Name,
                fileSize: fileInfo.Length,
                asyncStreamDelegate: async () => File.OpenRead(fileInfo.FullName),
                fileType: _mimeTypeDetection.TryGetFileType(fileInfo.FullName, out var mt) ? mt : IMimeTypeDetection.OCTET_STREAM_MIME_TYPE,
                lastWriteTimeUtc: fileInfo.LastWriteTimeUtc
            );
            return Task.FromResult(streamableFileContent);
        }
    }
}
