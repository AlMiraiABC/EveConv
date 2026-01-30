using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.Downloader;
using EveConv.Embedder;
using EveConv.Onnx;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EveConv.Connectors.Onnx
{
    public class OnnxEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        private bool _disposed = false;

        private readonly ILogger<OnnxEmbeddingGenerator> _logger;
        private readonly OnnxEmbeddingGeneratorOptions _config;
        private readonly OnnxTextEmbedder<float> _embedder;

        public OnnxEmbeddingGenerator(OnnxTextEmbedder<float> embedder, IOptions<OnnxEmbeddingGeneratorOptions> options, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(embedder);
            ArgumentNullException.ThrowIfNull(options);
            options.Value.Valid();
            _embedder = embedder;
            _config = options.Value;
            _logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<OnnxEmbeddingGenerator>();
        }

        public void Dispose()
        {
            if (_disposed) return;
            GC.SuppressFinalize(this);
            _disposed = true;
            _embedder.Dispose();
        }

        public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(values);
            var vs = values.ToArray();
            if (vs.Length == 0)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("No input values provided for embedding generation.");
                }
                return [];
            }
            var ctx = EmbeddingGenerationExtensions.ToOptions(options);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Starting generate embeddings for {count} values", vs.Length);
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("Starting generate embeddings with {options} for {values}",
                        JsonSerializer.Serialize(ctx),
                        JsonSerializer.Serialize(vs));
                }
            }
            var embeddings = await _embedder.BatchEmbeddingAsync(vs, ctx, cancellationToken);
            var normalized = await _embedder.NormalizeAsync(embeddings, ctx, cancellationToken);
            var dim0 = normalized.GetLength(0);
            var dim1 = normalized.GetLength(1);
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Got embeddings with dimensions: [{dim0}, {dim1}]", dim0, dim1);
            }
            var result = new GeneratedEmbeddings<Embedding<float>>(dim0);
            for (int i = 0; i < dim0; i++)
            {
                var row = new float[dim1];
                for (int j = 0; j < dim1; j++)
                {
                    row[j] = normalized[i, j];
                }
                var embedding = new Embedding<float>(row);
                result.Add(embedding);
            }
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Generated {count} embeddings", result.Count);
            }
            return result;
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            ArgumentNullException.ThrowIfNull(serviceType);
            if (serviceKey is null)
            {
                return null;
            }
            if (serviceType.IsInstanceOfType(this))
            {
                return this;
            }
            if (serviceType.IsInstanceOfType(_embedder))
            {
                return _embedder;
            }
            return null;
        }
    }
}
