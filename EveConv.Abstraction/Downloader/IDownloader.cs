using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.Downloader
{
    public interface IDownloader
    {
        Task<StreamableFileContent> DownloadAsync(string filePath, CancellationToken token = default);
    }
}
