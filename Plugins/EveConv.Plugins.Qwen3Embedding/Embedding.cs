using System;
using System.Collections.Generic;
using System.Text;
using EveConv.Abstraction;
using EveConv.Connectors.Onnx;
using EveConv.Downloader;
using EveConv.HuggingFace;
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

        public async Task<Embedding<float>> GetEmbeddingAsync(string input,
            CancellationToken cancellationToken = default)
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

        public async Task<GeneratedEmbeddings<Embedding<float>>> GetEmbeddingsAsync(IEnumerable<string> input,
            CancellationToken cancellationToken = default)
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
            const string org = "onnx-community";
            var repo = $"Qwen3-Embedding-{_config.Model}-ONNX";
            var saveFolder = Path.Combine(Context.InstallPath, RES_FOLDER);
            if (!Directory.Exists(saveFolder))
            {
                Directory.CreateDirectory(saveFolder);
            }
            var downloader = new HFDownloader(new HuggingFace.Config()
            {
                AuthorizationToken = this._config.HFToken,
                Endpoint = this._config.HFEndpoint,
                Proxy = this._config.HFProxy,
                ExtraHeaders = this._config.HFExtraHeaders,
            });
            var modelSavePath = Path.Combine(saveFolder, _config.ModelFileName);
            var fp = $"onnx/model_{_config.Quantized}.onnx";
            await downloader.DownloadFileAsync(org, repo, fp, modelSavePath, true,
                progressEmit: _logger.IsEnabled(LogLevel.Debug)
                    ? (_, e) =>
                    {
                        _logger.LogDebug("Downloading {org}/{repo}/{file} ... {percentage}({downloaded}/{total})",
                            e.Argument.Organization, e.Argument.Repository, e.Argument.FilePath, e.Percentage,
                            e.DownloadedSize, e.TotalSize);
                    }
                    : null,
                cancellationToken: cancellationToken);
            var tokenizerSavePath = Path.Combine(saveFolder, _config.TokenizerJsonFileName);
            fp = "tokenizer.json";
            await downloader.DownloadFileAsync(org, repo, fp, tokenizerSavePath, true,
                cancellationToken: cancellationToken);
        }
    }
}
