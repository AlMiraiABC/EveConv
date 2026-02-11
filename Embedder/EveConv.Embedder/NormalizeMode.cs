using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Numerics.Tensors;
using System.Text;

namespace EveConv.Embedder
{
    /// <summary>
    /// Create final(dimension reduction) sentence embeddings.
    /// </summary>
    /// <seealso cref="https://github.com/microsoft/semantic-kernel/blob/838b9511723b32e34956370313d9466045f9a453/dotnet/src/Connectors/Connectors.Onnx/PoolingMode.cs"/>
    /// <remarks>Low 4-bits used to dimension reduction; and high 4-bits used to scalling.</remarks>
    [Flags]
    public enum NormalizeMode
    {
        /// <summary>
        /// Uses the maximum across all token embeddings.
        /// </summary>
        Max = 0x01,
        /// <summary>
        /// Calculates the average across all token embeddings.
        /// </summary>
        Mean = 0x02,
        /// <summary>
        /// Calculates the average across all token embeddings, divided by the square root of the number of tokens.
        /// </summary>
        MeanSquareRootTokensLength = 0x03,
        /// <summary>
        /// Calculates the sum across all token embeddings.
        /// </summary>
        Sum = 0x04,

        /// <summary>
        /// Scalling to <c>1</c> with Manhattan Distance.
        /// </summary>
        /// <remarks><c>X=sum(abs(x_i)); x_i=x_i/X;</c></remarks>
        L1 = 0x10,
        /// <summary>
        /// Scalling to <c>1</c> with Euclidean norm.
        /// </summary>
        /// <remarks><c>X=sqrt(sum(x_i^2)); x_i=x_i/X;</c></remarks>
        L2 = 0x20,
        /// <summary>
        /// Scalling to <c>[0, 1]</c> with Min Max Scalling.
        /// </summary>
        /// <remarks><c>x_i=(x_i-min)/(max-min)</c></remarks>
        MinMaxScalling01 = 0x30,
        /// <summary>
        /// Scalling to <c>[-1, 1]</c> with Min Max Scalling.
        /// </summary>
        /// <remarks><c>x_i=(x_i-mean)/(max-min)</c></remarks>
        MinMaxScallingMean = 0x40,
        /// <summary>
        /// Scalling with Z-Score.
        /// </summary>
        /// <remarks><c>x_i=(x_i-mean)/std</c></remarks>
        ZScore = 0x50,
    }

#pragma warning disable SYSLIB5001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
    internal static class NormalizeImpl<V> where V : INumber<V>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="embeddings">Embedding data.</param>
        /// <param name="mode">Default mode is <c>0x02</c></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">Model type is not supported.</exception>
        public static V[][] Normalize(V[,,] embeddings, NormalizeMode mode)
        {
            var dim0 = embeddings.GetLength(0);
            var dim1 = embeddings.GetLength(1);
            var dim2 = embeddings.GetLength(2);
            if (dim0 == 0 || dim1 == 0 || dim2 == 0)
            {
                return [];
            }
            var lmode = mode & (NormalizeMode)0x0F;
            var hmode = mode & (NormalizeMode)0xF0;
            var results = new V[dim0][];
            var input = Tensor.Create(embeddings.Cast<V>(), new ReadOnlySpan<nint>([dim0, dim1, dim2]));
            for (int i = 0; i < dim0; i++)
            {
                var span = input.Slice([new NRange(i..(i + 1)), new NRange(0..), new NRange(0..)]);
                var reshape = span.Reshape(dim1, dim2);
                results[i] = lmode switch
                {
                    NormalizeMode.Max => Max(reshape),
                    0x00 or NormalizeMode.Mean => Mean(reshape, false),
                    NormalizeMode.MeanSquareRootTokensLength => Mean(reshape, true),
                    NormalizeMode.Sum => Sum(reshape),
                    _ => throw new NotSupportedException($"Unsupported NormalizeMode: {mode}"),
                };
                if (hmode == 0)
                {
                    continue;
                }
                results[i] = hmode switch
                {
                    NormalizeMode.L1 => L1(results[i]),
                    NormalizeMode.L2 => L2(results[i]),
                    NormalizeMode.MinMaxScalling01 => MinMaxScaling(results[i], false),
                    NormalizeMode.MinMaxScallingMean => MinMaxScaling(results[i], true),
                    NormalizeMode.ZScore => ZScore(results[i]),
                    _ => throw new NotSupportedException($"Unsupported NormalizeMode: {mode}"),
                };
            }
            return results;
        }

        #region lmode

        private static V[] Max(ReadOnlyTensorSpan<V> embedding)
        {
            if (embedding.Rank! > 2)
            {
                throw new ArgumentException($"Embedding tensor rank must be 2, but got {embedding.Rank}");
            }
            var result = new V[embedding.Lengths[1]];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = Tensor.Max(embedding.Slice([new NRange(0..), new NRange(i..(i + 1))]));
            }
            return result;
        }

        private static V[] Mean(ReadOnlyTensorSpan<V> embedding, bool sqrt)
        {
            if (embedding.Rank! > 2)
            {
                throw new ArgumentException($"Embedding tensor rank must be 2, but got {embedding.Rank}");
            }
            var result = Sum(embedding);
            var y = (double)embedding.Lengths[0];
            if (sqrt)
            {
                if (typeof(V) == typeof(double))
                {
                    y = Math.Sqrt(y);
                }
                else
                {
                    y = MathF.Sqrt((float)y);
                }
            }
            TensorPrimitives.Divide(result, V.CreateChecked(y), result);
            return result;
        }

        private static V[] Sum(ReadOnlyTensorSpan<V> embedding)
        {
            if (embedding.Rank! > 2)
            {
                throw new ArgumentException($"Embedding tensor rank must be 2, but got {embedding.Rank}");
            }
            var result = new V[embedding.Lengths[1]];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = Tensor.Sum(embedding.Slice([new NRange(0..), new NRange(i..(i + 1))]));
            }
            return result;
        }

        #endregion

        #region hmode

        private static V[] L1(V[] embedding)
        {
            var result = new V[embedding.Length];
            var sum = TensorPrimitives.SumOfMagnitudes(embedding);
            TensorPrimitives.Divide(embedding, sum, result);
            return result;
        }

        private static V[] L2(V[] embedding)
        {
            var result = new V[embedding.Length];
            var sum = TensorPrimitives.SumOfSquares(embedding);
            var sqrt = V.CreateChecked(Math.Sqrt(double.CreateChecked(sum)));
            TensorPrimitives.Divide(embedding, sqrt, result);
            return result;
        }

        private static V[] MinMaxScaling(V[] embedding, bool isMean)
        {
            var result = new V[embedding.Length];
            var max = TensorPrimitives.Max(embedding);
            var min = TensorPrimitives.Min(embedding);
            var s = min;
            if (isMean)
            {
                var sum = TensorPrimitives.Sum(embedding);
                s = sum / V.CreateChecked(embedding.Length);
            }
            TensorPrimitives.Subtract(embedding, s, result);
            TensorPrimitives.Divide(result, max - min, result);
            return result;
        }

        private static V[] ZScore(V[] embedding)
        {
            var result = new V[embedding.Length];
            var mean = TensorPrimitives.Sum(embedding) / V.CreateChecked(embedding.Length);
            // x_i=(x_i-mean)/std; std=sqrt(sum((x_i-mean)^2)/n)
            TensorPrimitives.Subtract(embedding, mean, result);
            var sum = TensorPrimitives.SumOfSquares(result);
            var std = V.CreateChecked(Math.Sqrt(double.CreateChecked(sum / V.CreateChecked(embedding.Length))));
            TensorPrimitives.Subtract(embedding, mean, result);
            TensorPrimitives.Divide(result, std, result);
            return result;
        }

        #endregion
    }
#pragma warning restore SYSLIB5001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
}
