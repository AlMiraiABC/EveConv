using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.Downloader;
using EveConv.Abstraction.ModelExecutor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;

namespace EveConv.Onnx
{
    public class OnnxExecutor : ModelInferable<SessionOptions, OnnxInferenceSession>
    {
        private readonly ILogger<OnnxExecutor> _logger;
        private readonly OnnxExecutorConfiguration _config;
        private readonly string[] _availableEps;

        public OnnxExecutor(IOptions<OnnxExecutorConfiguration> options, IDownloader? downloader, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(options);
            options.Value.Valid();
            this._config = options.Value;
            this._logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<OnnxExecutor>();
            this._downloader = downloader;
            _availableEps = options.Value.DefaultEps.Length == 0
                ? OrtEnv.Instance().GetAvailableProviders()
                : options.Value.DefaultEps;
        }

        /// <summary>
        /// Create an inference session from model binary with default options.
        /// </summary>
        /// <param name="model">Model file path.</param>
        /// <param name="name">
        ///     Model name used for optimization cache. Should be unique.
        ///     <para/>
        ///     It is suggested to use model hash or versioned name.
        /// </param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A task that has an inference session.</returns>
        public async Task<OnnxInferenceSession> CreateSessionAsync(string model, string name, CancellationToken token = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            return await CreateSessionAsync(model, CreateDefaultSessionOptions(name), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Create an inference session from model binary with default options.
        /// </summary>
        /// <param name="model">Model content.</param>
        /// <param name="name">
        ///     Model name used for optimization cache. Should be unique.
        ///     <para/>
        ///     It is suggested to use model hash or versioned name.
        /// </param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A task that has an inference session.</returns>
        public async Task<OnnxInferenceSession> CreateSessionAsync(byte[] model, string name, CancellationToken token = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            return await CreateSessionAsync(model, CreateDefaultSessionOptions(name), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Create an inference session from model file with options from factory.
        /// </summary>
        /// <param name="model">Model file path.</param>
        /// <param name="factory">Session options builder.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A task that has an inference session.</returns>
        public async Task<OnnxInferenceSession> CreateSessionAsync(string model, Func<string[], CancellationToken, Task<SessionOptions>> factory, CancellationToken token = default)
        {
            return await CreateSessionAsync(model, await factory(_availableEps, token), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Create an inference session from model binary with options from factory.
        /// </summary>
        /// <param name="model">Model content.</param>
        /// <param name="factory">Session options builder.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A task that has an inference session.</returns>
        public async Task<OnnxInferenceSession> CreateSessionAsync(byte[] model, Func<string[], CancellationToken, Task<SessionOptions>> factory, CancellationToken token = default)
        {
            return await CreateSessionAsync(model, await factory(_availableEps, token), token).ConfigureAwait(false);
        }

        public override async Task<OnnxInferenceSession> CreateSessionAsync(Stream model, SessionOptions? options, CancellationToken token = default)
        {
            options ??= new();
            using var memoryStream = new MemoryStream();
            await model.CopyToAsync(memoryStream, token);
            var bytes = memoryStream.ToArray();
            return await CreateSessionAsync(bytes, options, token);
        }


        /// <summary>
        /// Create an inference session from model binary with given options.
        /// </summary>
        /// <param name="model">Model content.</param>
        /// <param name="options">Session options.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A task that has an inference session.</returns>
#pragma warning disable IDE0060 // 删除未使用的参数
        public async Task<OnnxInferenceSession> CreateSessionAsync(byte[] model, SessionOptions options, CancellationToken token = default)
#pragma warning restore IDE0060 // 删除未使用的参数
        {
            var session = new InferenceSession(model, options);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("ONNX Runtime Inference Session created");
            }
            return new(session);
        }

        private readonly static string[] ExcludedEps = ["CPUExecutionProvider"];
        private SessionOptions CreateDefaultSessionOptions(string name)
        {
            var opt = new SessionOptions()
            {
                ExecutionMode = _config.DefaultExecutionMode,
                GraphOptimizationLevel = _config.DefaultGraphOptimizationLevel,
                OptimizedModelFilePath = Path.Join(_config.DefaultOptimizedModelSaveFolder, $"{name}{_config.DefaultOptimizedModelSaveExtension}"),
            };
            foreach (var ep in _availableEps.Except(ExcludedEps))
            {
                opt.AppendExecutionProvider(ep);
            }
            return opt;
        }
    }
}
