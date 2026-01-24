using System;
using System.Collections.Generic;
using System.Text;
using EveConv.Abstraction.ModelExecutor;
using EveConv.Abstraction.Tokenizer;
using EveConv.Embedder;
using EveConv.Onnx;

namespace EveConv.Downloader.Tests
{
    /// <summary>
    /// bge-small-zh-v1.5 embedder.
    /// </summary>
    internal class BgeSmallZhV1_5Embedder : OnnxTextEmbedder<float>
    {
        public BgeSmallZhV1_5Embedder(IModelInference modelInference, string modelPath, ITextTokenizer<long> tokenizer) : base(modelInference, modelPath, tokenizer)
        {
        }

        protected override async Task<float[,,]> GenerateEmbeddingAsync(long[][] inputTokens, OnnxInferenceSession session, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default)
        {
            var tokens = inputTokens.ToMatrix();
            var info = await session.ExecuteAsync(new Dictionary<string, Array?>()
            {
                { "input_ids", tokens },
                { "attention_mask",  GetAttentionMask(tokens) },
                { "token_type_ids", GetTokenTypeIds(tokens) },
            }, token: cancellationToken);
            return await RetrieveEmbeddingAsync(info);
        }

        private static long[,] GetAttentionMask(long[,] tokens)
        {
            var dim0 = tokens.GetLength(0);
            var dim1 = tokens.GetLength(1);
            var mask = new long[dim0, dim1];
            for (var i = 0; i < dim0; i++)
            {
                for (var j = 0; j < dim1; j++)
                {
                    mask[i, j] = tokens[i, j] != 0 ? 1 : 0;
                }
            }
            return mask;
        }

        private static long[,] GetTokenTypeIds(long[,] tokens)
        {
            var dim0 = tokens.GetLength(0);
            var dim1 = tokens.GetLength(1);
            var typeIds = new long[dim0, dim1];
            return typeIds;
        }
    }
}
