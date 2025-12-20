using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using EveConv.Abstraction.Downloader;

namespace EveConv.Abstraction.ModelExecutor
{
    public abstract class ModelInferable<O, S> : IModelInference<O, S>
        where O: class
        where S : IInferenceSession
    {
        protected IDownloader? _downloader;

        public async Task<IInferenceSession> CreateSessionAsync(string modelPath, CancellationToken token = default)
        {
            var file = await DownloadModelAsync(modelPath, token).ConfigureAwait(false);
            var stream = await file.GetStreamAsync().ConfigureAwait(false);
            return await CreateSessionAsync(stream, token);
        }

        public async Task<IInferenceSession> CreateSessionAsync(Stream model, CancellationToken token = default)
        {
            return await CreateSessionAsync(model, null, token);
        }

        public async Task<S> CreateSessionAsync(string modelPath, O? options, CancellationToken token = default)
        {
            var file = await DownloadModelAsync(modelPath, token).ConfigureAwait(false);
            var stream = await file.GetStreamAsync().ConfigureAwait(false);
            return await CreateSessionAsync(stream, options, token);
        }

        public abstract Task<S> CreateSessionAsync(Stream model, O? options, CancellationToken token = default);

        protected async Task<StreamableFileContent> DownloadModelAsync(string filePath, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(_downloader);
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            return await _downloader.DownloadAsync(filePath, token)
                ?? throw new FileNotFoundException("Download failed with no content.", filePath);
        }

        protected async Task<string> DownloadModelAsync(string filePath, string saveDir, CancellationToken token = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(saveDir);
            Directory.CreateDirectory(saveDir);
            var file = await DownloadModelAsync(filePath, token);
            var p = Path.Join(saveDir, string.IsNullOrWhiteSpace(file.FileName) ? Guid.NewGuid().ToString() : file.FileName);
            using var memoryStream = new MemoryStream();
            using var sourceStream = await file.GetStreamAsync().ConfigureAwait(false);
            using var fileStream = File.Create(p);
            await sourceStream.CopyToAsync(fileStream, token).ConfigureAwait(false);
            return p;
        }
    }
}
