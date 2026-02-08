using System.Numerics;
using System.Threading.Tasks;
using EveConv.Abstraction.Embedder;
using EveConv.Abstraction.ModelExecutor;
using EveConv.Abstraction.Tokenizer;

namespace EveConv.Embedder
{
    public delegate Task<V[,]> NormalizeAsyncCallback<V>(V[,,] embeddings, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default)
        where V : struct, INumber<V>;

    public abstract class TextEmbedder<V> : ITextEmbedder<V>, IDisposable
        where V : struct, INumber<V>
    {
        protected readonly IModelInference _modelInference;
        protected readonly string _modelPath;
        protected readonly ITextTokenizer<long> _tokenizer;
        protected IInferenceSession? _inferenceSession;
        protected NormalizeAsyncCallback<V> _normalizAsyncCallback;

        protected bool _disposed = false;

        protected TextEmbedder(IModelInference modelInference, string modelPath, ITextTokenizer<long> tokenizer)
            : this(modelInference, modelPath, tokenizer, NormalizeMode.Mean)
        {
        }

        protected TextEmbedder(IModelInference modelInference, string modelPath, ITextTokenizer<long> tokenizer,
            NormalizeMode normalizeMode)
        {
            ArgumentNullException.ThrowIfNull(modelInference);
            ArgumentException.ThrowIfNullOrEmpty(modelPath);
            this._modelInference = modelInference;
            this._modelPath = modelPath;
            this._tokenizer = tokenizer;
            _normalizAsyncCallback = async (embeddings, context, cancellationToken) =>
            {
                var normalized = NormalizeImpl<V>.Normalize(embeddings, normalizeMode);
                var dimx = normalized.Length;
                var dimy = normalized.Max(i => i.Length);
                var matrix = new V[dimx, dimy];
                for (var i = 0; i < dimx; i++)
                {
                    for (var j = 0; j < dimy; j++)
                    {
                        matrix[i, j] = normalized[i][j];
                    }
                }
                return matrix;
            };
        }

        protected TextEmbedder(IModelInference modelInference, string modelPath, ITextTokenizer<long> tokenizer,
            NormalizeAsyncCallback<V> normalizeAsyncCallback) : this(modelInference, modelPath, tokenizer)
        {
            if (normalizeAsyncCallback is not null)
            {
                _normalizAsyncCallback = normalizeAsyncCallback;
            }
        }

        /// <summary>
        /// Creates or retrieves an existing inference session form the specified model path.
        /// </summary>
        /// <param name="recreate">Determine whether re-create the inference session.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task that contains an exists or created inference session.</returns>
        protected async Task<IInferenceSession> GetInferenceSessionAsync(bool recreate = false, CancellationToken token = default)
        {
            CheckDisposed();
            if (recreate && _inferenceSession is not null)
            {
                _inferenceSession.Dispose();
            }
            if (_inferenceSession is not null)
            {
                return _inferenceSession;
            }
            _inferenceSession = await _modelInference.CreateSessionAsync(_modelPath, token).ConfigureAwait(false);
            return _inferenceSession;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            GC.SuppressFinalize(this);
            _inferenceSession?.Dispose();
            _inferenceSession = null;
            this._disposed = true;
        }

        public virtual async Task<V[,]> EmbeddingAsync(string input, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            var tokens = await this._tokenizer.TokenizeAsync(input, context, cancellationToken).ConfigureAwait(false);
            var session = await GetInferenceSessionAsync(token: cancellationToken).ConfigureAwait(false);
            return await GenerateEmbeddingAsync(tokens, session, context, cancellationToken).ConfigureAwait(false);
        }

        public virtual async Task<V[,,]> BatchEmbeddingAsync(string[] inputs, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            var tokens = await this._tokenizer.TokenizeBatchAsync(inputs, context, cancellationToken).ConfigureAwait(false);
            var session = await GetInferenceSessionAsync(token: cancellationToken).ConfigureAwait(false);
            var embeddings = await Task.WhenAll(Array.ConvertAll(tokens, async t => await GenerateEmbeddingAsync(t, session, context, cancellationToken).ConfigureAwait(false))).ConfigureAwait(false);
            var xl = embeddings.Length;
            var yl = embeddings.Max(x => x.GetLength(0));
            var zl = embeddings.Max(x => x.GetLength(1));
            var matrix = new V[xl, yl, zl];
            for (var i = 0; i < embeddings.Length; i++)
            {
                var embedding = embeddings[i];
                for (var j = 0; j < embedding.GetLength(0); j++)
                {
                    for (var k = 0; k < embedding.GetLength(1); k++)
                    {
                        matrix[i, j, k] = embedding[j, k];
                    }
                }
            }
            return matrix;
        }

        public virtual async Task<long[]> GetTokensAsync(string inputs, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            return await this._tokenizer.TokenizeAsync(inputs, context, cancellationToken).ConfigureAwait(false);
        }

        public virtual async Task<long[][]> GetTokensAsync(string[] inputs, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            return await this._tokenizer.TokenizeBatchAsync(inputs, context, cancellationToken).ConfigureAwait(false);
        }

        public virtual async Task<V[,]> NormalizeAsync(V[,,] embeddings, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default)
        {
            return await _normalizAsyncCallback(embeddings, context, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously executes the inference session to generate embedding and converts the result to the specified type.
        /// </summary>
        /// <param name="inputTokens">The specified input tokens.</param>
        /// <param name="session">The specified inference session.</param>
        /// <param name="context">External context.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that contains embedded result.</returns>
        protected abstract Task<V[,]> GenerateEmbeddingAsync(long[] inputTokens, IInferenceSession session, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously executes the inference session to generate embedding and converts the result to the specified type.
        /// </summary>
        /// <param name="inputTokens">A set of input tokens.</param>
        /// <param name="session">The specified inference session.</param>
        /// <param name="context">External context.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that contains embedded results.</returns>
        protected abstract Task<V[,,]> GenerateEmbeddingAsync(long[][] inputTokens, IInferenceSession session, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default);

        protected void CheckDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

    }
}
