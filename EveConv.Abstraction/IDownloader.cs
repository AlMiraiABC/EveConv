using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction
{
    public interface IDownloader
    {
        Task<Stream> DownloadAsync(string filePath, CancellationToken token = default);
    }
}
