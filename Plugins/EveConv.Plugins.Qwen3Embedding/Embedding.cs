using System;
using System.Collections.Generic;
using System.Text;
using EveConv.Abstraction;
using EveConv.Connectors.Onnx;
using EveConv.Downloader;
using EveConv.HuggingFaceFastTokenizer;
using EveConv.MimeType;
using EveConv.Onnx;
using EveConv.Plugins.Api;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;

namespace EveConv.Plugins.Qwen3Embedding
{
    public partial class Plugin : ITextEmbedding, ITextEmbeddingAsync
    {
        private OnnxEmbeddingGenerator? _generator;
        private OnnxExecutor? _modelInference;

        private const string RES_FOLDER = "Resources";
        private const string CACHE_FOLDER = "Cache";

        public Embedding<float> GetEmbedding(string input)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentException.ThrowIfNullOrEmpty(input);
            var embeddings = GetEmbeddings([input]);
            if (embeddings.Count == 0)
            {
                throw new IndexOutOfRangeException($"Generate embedding failed for input `{input}`");
            }
            return embeddings[0];
        }

        public async Task<Embedding<float>> GetEmbeddingAsync(string input, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentException.ThrowIfNullOrEmpty(input);
            var embeddings = await GetEmbeddingsAsync([input], cancellationToken);
            if (embeddings.Count == 0)
            {
                throw new IndexOutOfRangeException($"Generate embedding failed for input `{input}`");
            }
            return embeddings[0];
        }

        public GeneratedEmbeddings<Embedding<float>> GetEmbeddings(IEnumerable<string> input)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var generator = GetGenerator().GetAwaiter().GetResult();
            return generator.GenerateAsync(input).GetAwaiter().GetResult();
        }

        public async Task<GeneratedEmbeddings<Embedding<float>>> GetEmbeddingsAsync(IEnumerable<string> input, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var generator = await GetGenerator(cancellationToken);
            return await generator.GenerateAsync(input, null, cancellationToken);
        }

        private readonly SemaphoreSlim _getGeneratorLock = new(1, 1);
        private async Task<OnnxEmbeddingGenerator> GetGenerator(CancellationToken cancellationToken = default)
        {
            if (_generator is not null)
            {
                return _generator;
            }
            await _getGeneratorLock.WaitAsync(cancellationToken);
            try
            {
                if (_generator is not null) // double check after acquire lock
                {
                    return _generator;
                }
                var resourcePath = Path.Combine(Context.InstallPath, RES_FOLDER);
                var tokenizerJsonPath = Path.Combine(resourcePath, _config.TokenizerJsonFileName);
                var modelPath = Path.Combine(resourcePath, _config.ModelFileName);
                await DownloadModelFile(cancellationToken);
                if (!File.Exists(tokenizerJsonPath))
                {
                    throw new FileNotFoundException("Tokenizer json file not found", tokenizerJsonPath);
                }
                if (!File.Exists(modelPath))
                {
                    throw new FileNotFoundException("Model file not found", modelPath);
                }
                var tokenizer = new HFFastTokenizer(new HFFastTokenizerConfiguration()
                {
                    TokenizerJsonPath = tokenizerJsonPath,
                }, _loggerFactory);
                if (_modelInference is null)
                {
                    var graphOptLevel = Enum.Parse<GraphOptimizationLevel>(_config.OrtGraphOptimizationLevel);
                    var saveFolder = Path.IsPathRooted(_config.OrtOptimizedSaveFolder)
                        ? _config.OrtOptimizedSaveFolder
                        : Path.Combine(Context.InstallPath, CACHE_FOLDER, _config.OrtOptimizedSaveFolder);
                    var saveExt = _config.OrtOptimizedSaveExtension;
                    var downloader = new LocalDownloader(new MimeTypesDetection());
                    _modelInference = new OnnxExecutor(new OnnxExecutorConfiguration()
                    {
                        DefaultGraphOptimizationLevel = graphOptLevel,
                        DefaultOptimizedModelSaveFolder = saveFolder,
                        DefaultOptimizedModelSaveExtension = saveExt,
                    }, downloader, _loggerFactory);
                }
                var embedder = new Qwen3Embedder(_modelInference, modelPath, tokenizer);
                _generator = new OnnxEmbeddingGenerator(embedder, new OnnxEmbeddingGeneratorOptions(), _loggerFactory);
                return _generator;
            }
            finally
            {
                _getGeneratorLock.Release();
            }
        }

        private async Task DownloadModelFile(CancellationToken cancellationToken = default)
        {
            var repoName = $"onnx-community/Qwen3-Embedding-{_config.Model}-ONNX";
            var resourcePath = Path.Combine(Context.InstallPath, RES_FOLDER);
            if (!Directory.Exists(resourcePath))
            {
                Directory.CreateDirectory(resourcePath);
            }
            var modelSavePath = Path.Combine(resourcePath, _config.ModelFileName);
            Dictionary<string, string> headers = [];
            var hf_token = this._config.HFToken;
            if (!string.IsNullOrWhiteSpace(hf_token))
            {
                headers.Add("Authorization", $"Bearer {hf_token}");
            }
            if (this._config.HFExtraHeaders is not null)
            {
                foreach (var (k, v) in this._config.HFExtraHeaders)
                {
                    headers.TryAdd(k, v);
                }
            }
            var downloader = new HttpDownloader(new HttpConfiguration()
            {
                RequestHeaders = headers,
                HttpProxy = this._config.HFProxy,
            }, _loggerFactory);
            var url = string.Format(this._config.HfDownloadUrl, repoName, $"onnx/model_{_config.Quantized}.onnx");
            await Download(url, modelSavePath, cancellationToken);
            var tokenizerSavePath = Path.Combine(resourcePath, _config.TokenizerJsonFileName);
            url = string.Format(this._config.HfDownloadUrl, repoName, "tokenizer.json");
            await Download(url, tokenizerSavePath, cancellationToken);

            async Task Download(string url, string savePath, CancellationToken cancellationToken)
            {
                if (File.Exists(savePath))
                {
                    return;
                }
                try
                {
                    var response = await downloader.DownloadAsync(url, cancellationToken);
                    using var stream = await response.GetStreamAsync();
                    using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write);
                    await stream.CopyToAsync(fileStream, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to download file from url `{Url}` to path `{Path}`. Deleting partial file.", url, savePath);
                    if (File.Exists(savePath))
                    {
                        File.Delete(savePath);
                    }
                    throw;
                }
            }
        }

    }
}
