using System;
using System.Collections.Generic;
using System.Text;
using EveConv.Abstraction.ModelExecutor;
using EveConv.Abstraction.Tokenizer;
using EveConv.Embedder;
using EveConv.Onnx;

namespace EveConv.Plugins.Qwen3Embedding
{
    internal class Qwen3Embedder : OnnxTextEmbedder<float>
    {
        private const PaddingSide PADDING_SIDE = PaddingSide.Left;
        private const long PAD_TOKEN_ID = 151643; // <|endoftext|>
        private const int KV_CACHE_LEN = 27;
        private const NormalizeMode NORMALIZE_MODE = NormalizeMode.PadLeftLast | NormalizeMode.L2;

        public Qwen3Embedder(IModelInference modelInference, string modelPath, ITextTokenizer<long> tokenizer)
            : base(modelInference, modelPath, tokenizer, NORMALIZE_MODE)
        {
        }

        protected override async Task<float[,,]> GenerateEmbeddingAsync(long[][] inputTokens, OnnxInferenceSession session, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default)
        {
            var tokens = InputIdsWithEos(inputTokens);
            var input = new Dictionary<string, Array?>()
            {
                { "input_ids", tokens },
                { "attention_mask",  GetAttentionMask(tokens) },
                { "position_ids", GetPositionIds(tokens) },
            };
            for (int i = 0; i <= KV_CACHE_LEN; i++)
            {
                input.Add($"past_key_values.{i}.key", EmptyPastKeyValues(tokens));
                input.Add($"past_key_values.{i}.value", EmptyPastKeyValues(tokens));
            }
            var info = await session.ExecuteAsync(input, token: cancellationToken);
            return await RetrieveEmbeddingAsync(info);
        }

        private static long[,] InputIdsWithEos(long[][] inputTokens)
        {
            var tokens = new long[inputTokens.Length][];
            for (var i = 0; i < inputTokens.Length; i++)
            {
                if (inputTokens[i].Length == 0 || inputTokens[i][^1] == PAD_TOKEN_ID)
                {
                    tokens[i] = inputTokens[i];
                    continue;
                }
                tokens[i] = new long[inputTokens[i].Length + 1];
                Array.Copy(inputTokens[i], tokens[i], inputTokens[i].Length);
                tokens[i][inputTokens[i].Length] = PAD_TOKEN_ID; // Add EOS token at the end
            }
            return tokens.ToMatrix(PADDING_SIDE, PAD_TOKEN_ID);
        }

        private static long[,] GetAttentionMask(long[,] tokens)
        {
            var dim0 = tokens.GetLength(0);
            var dim1 = tokens.GetLength(1);
            var mask = new long[dim0, dim1];
            for (var i = 0; i < dim0; i++)
            {
                var fst = false;
                for (var j = 0; j < dim1; j++)
                {
                    if (!fst && tokens[i, j] == PAD_TOKEN_ID)
                    {
                        mask[i, j] = 0;
                        continue;
                    }
                    mask[i, j] = 1;
                    fst = true;
                }
            }
            return mask;
        }

        private static long[,] GetPositionIds(long[,] tokens)
        {
            var dim0 = tokens.GetLength(0);
            var dim1 = tokens.GetLength(1);
            var positionIds = new long[dim0, dim1];
            for (var i = 0; i < dim0; i++)
            {
                var fstid = 0;
                for (var j = 0; j < dim1; j++)
                {
                    if (tokens[i, j] != PAD_TOKEN_ID)
                    {
                        fstid = j;
                        break;
                    }
                }
                for (var j = fstid; j < dim1; j++)
                {
                    positionIds[i, j] = j - fstid;
                }
            }
            return positionIds;
        }

        private static float[,,,] EmptyPastKeyValues(long[,] tokens)
        {
            var dim0 = tokens.GetLength(0);
            return new float[dim0, 8, 0, 128];
        }
    }
}
