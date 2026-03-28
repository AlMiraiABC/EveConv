using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Threading;
using EveConv.Abstraction.ModelExecutor;
using EveConv.Abstraction.Tokenizer;
using EveConv.Onnx;

namespace EveConv.Embedder
{
    public abstract class OnnxTextEmbedder<V> : TextEmbedder<V>
                where V : struct, INumber<V>
    {
        protected OnnxTextEmbedder(IModelInference modelInference, string modelPath, ITextTokenizer<long> tokenizer, NormalizeMode normalizeMode = NormalizeMode.Mean)
            : base(modelInference, modelPath, tokenizer, normalizeMode)
        {
        }

        protected virtual async Task<OnnxInferenceSession> GetOnnxInferenceSessionAsync(bool recreate = false, CancellationToken token = default)
        {
            return await GetInferenceSessionAsync(token: token).ConfigureAwait(false) as OnnxInferenceSession
                ?? throw new InvalidCastException($"The inference session should be {nameof(OnnxInferenceSession)}.");
        }

        public override async Task<V[,]> EmbeddingAsync(string input, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(input))
            {
                return new V[0, 0];
            }
            var tokens = await GetTokensAsync(input, context, cancellationToken).ConfigureAwait(false);
            var session = await GetOnnxInferenceSessionAsync(token: cancellationToken).ConfigureAwait(false);
            return await GenerateEmbeddingAsync(tokens, session, context, cancellationToken).ConfigureAwait(false);
        }

        public override async Task<V[,,]> BatchEmbeddingAsync(string[] inputs, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            if (inputs == null || inputs.Length == 0)
            {
                return new V[0, 0, 0];
            }
            var tokens = await GetTokensAsync(inputs, context, cancellationToken).ConfigureAwait(false);
            if (tokens is null || tokens.Length == 0)
            {
                return new V[0, 0, 0];
            }
            var paddedTokens = PaddingTokens(tokens);
            var session = await GetOnnxInferenceSessionAsync(token: cancellationToken).ConfigureAwait(false);
            return await GenerateEmbeddingAsync(paddedTokens, session, context, cancellationToken);
        }

        protected override async Task<V[,]> GenerateEmbeddingAsync(long[] inputTokens, IInferenceSession session, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default)
        {
            var s = await GetOnnxInferenceSessionAsync(token: cancellationToken).ConfigureAwait(false);
            return await GenerateEmbeddingAsync(inputTokens, s, context, cancellationToken).ConfigureAwait(false);
        }

        protected override async Task<V[,,]> GenerateEmbeddingAsync(long[][] inputTokens, IInferenceSession session, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default)
        {
            var s = await GetOnnxInferenceSessionAsync(token: cancellationToken).ConfigureAwait(false);
            return await GenerateEmbeddingAsync(inputTokens, s, context, cancellationToken).ConfigureAwait(false);
        }

        protected virtual async Task<V[,]> GenerateEmbeddingAsync(long[] inputTokens, OnnxInferenceSession session, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default)
        {
            var ts = new long[][] { inputTokens };
            var result = await GenerateEmbeddingAsync(ts, session, context, cancellationToken).ConfigureAwait(false);
            if (result.Rank < 1)
            {
                return new V[0, 0];
            }
            var dim0 = result.GetLength(1);
            var dim1 = result.GetLength(2);
            var matrix = new V[dim0, dim1];
            for (int i = 0; i < dim0; i++)
            {
                for (int j = 0; j < dim1; j++)
                {
                    matrix[i, j] = result[0, i, j];
                }
            }
            return matrix;
        }

        protected abstract Task<V[,,]> GenerateEmbeddingAsync(long[][] inputTokens, OnnxInferenceSession session, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Padding the tokens to fit the model input shape for each iter.
        /// </summary>
        /// <param name="tokens">Input tokens to onnx.</param>
        /// <returns>Padded tokens.</returns>
        /// <remarks>Outputs should have the same length.</remarks>
        protected virtual long[][] PaddingTokens(long[][] tokens)
        {
            if (tokens.Length == 0)
            {
                return tokens;
            }
            var maxLength = tokens.Max(t => t.Length);
            var padded = new long[tokens.Length][];
            for (int i = 0; i < tokens.Length; i++)
            {
                padded[i] = new long[maxLength];
                Array.Copy(tokens[i], padded[i], tokens[i].Length);
            }
            return padded;
        }

        /// <summary>
        /// Asynchronously retrieve the last hidden state output as a 2D array.
        /// </summary>
        /// <param name="info">Onnx run result information.</param>
        /// <param name="lastHiddenStateName">Output name of the last hidden state.</param>
        /// <returns>A task that contains the last hidden state output as a 2D array.</returns>
        /// <exception cref="OnnxException"></exception>
        /// <remarks><paramref name="info"/> will be disposed.</remarks>
        protected virtual async Task<V[,,]> RetrieveEmbeddingAsync(OnnxInferenceSessionInformation info, string lastHiddenStateName = "last_hidden_state")
        {
            ArgumentNullException.ThrowIfNull(info);
            if (info.Outputs is not null
                && info.Outputs.TryGetValue(lastHiddenStateName, out var vs)
                && vs is not null
                && vs.Length != 0)
            {
                Debug.Assert(vs.Rank == 3);
                info.Dispose();
                return (V[,,])vs;
            }
            if (info.OutputValues is null || info.OutputValues.Count == 0)
            {
                throw new OnnxException("The inference session information has no output values.");
            }
            var v = info.OutputValues.Where(v => v.Name == lastHiddenStateName).FirstOrDefault()
                ?? throw new OnnxException($"The output '{lastHiddenStateName}' is missing in the inference outputs.");
            var result = OnnxValueExtension.ToArray(v)
                ?? throw new OnnxException($"The output '{lastHiddenStateName}' is null.");
            return (V[,,])result;
        }
    }
}
