using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Embedder.Tests
{
    public class NormalizeModeTests
    {
        [Fact]
        public void Max_ShouldReturnMaxValues_Success()
        {
            var data = new float[,,]
            {
                {
                    { 1f, 2f, 3f },
                    { 4f, 5f, 6f },
                }
            };
            var actual = NormalizeImpl<float>.Normalize(data, NormalizeMode.Max);
            var expected = new float[][] { [4f, 5f, 6f] };
            Check(expected, actual);
        }

        [Fact]
        public void Mean_ShouldReturnMeanValues_Success()
        {
            var data = new float[,,]
            {
                {
                    { 1f, 2f, 3f },
                    { 4f, 5f, 6f },
                }
            };
            var actual = NormalizeImpl<float>.Normalize(data, NormalizeMode.Mean);
            var expected = new float[][] { [2.5f, 3.5f, 4.5f] };
            Check(expected, actual);
        }

        [Fact]
        public void Sum_ShouldReturnSumValues_Success()
        {
            var data = new float[,,]
            {
                {
                    { 1f, 2f, 3f },
                    { 4f, 5f, 6f },
                }
            };
            var actual = NormalizeImpl<float>.Normalize(data, NormalizeMode.Sum);
            var expected = new float[][] { [5f, 7f, 9f] };
            Check(expected, actual);
        }

        [Fact]
        public void MeanSquareRootTokensLength_ShouldReturnMeanValuesDividedBySqrtTokensLength_Success()
        {
            var data = new float[,,]
            {
                {
                    { 1f, 2f, 3f },
                    { 4f, 5f, 6f },
                }
            };
            var actual = NormalizeImpl<float>.Normalize(data, NormalizeMode.MeanSquareRootTokensLength);
            var expected = new float[][] { [3.5355339f, 4.9497475f, 6.3639610f] };
            Check(expected, actual);
        }

        [Fact]
        public void PadLeftLast_ShouldReturnPadLeftLast_Success()
        {
            var data = new float[,,]
            {
                {
                    { 1f, 2f, 3f },
                    { 4f, 5f, 6f },
                }
            };
            var actual = NormalizeImpl<float>.Normalize(data, NormalizeMode.PadLeftLast);
            var expected = new float[][] { [4f, 5f, 6f] };
            Check(expected, actual);
        }

        [Fact]
        public void Empty_ShouldReturnEmpty_Success()
        {
            var data = new float[,,] { };
            var actual = NormalizeImpl<float>.Normalize(data, NormalizeMode.Mean);
            var expected = Array.Empty<float[]>();
            Check(expected, actual);
        }

        private static void Check(float[][] expected, float[][] actual, float prec = 1e-5f)
        {
            Assert.Equal(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                var e = expected[i];
                var a = actual[i];
                Assert.Equal(e.Length, a.Length);
                for (int j = 0; j < e.Length; j++)
                {
                    Assert.Equal(e[j], a[j], prec);
                }
            }
        }

        [Fact]
        public void L1_ShouldReturnL1_Success()
        {
            var data = new float[,,]
            {
                {
                    { 1f, 2f, 3f },
                    { 4f, 5f, 6f },
                }
            };
            var actual = NormalizeImpl<float>.Normalize(data, NormalizeMode.Mean | NormalizeMode.L1);
            // X = 10.5f
            var expected = new float[][] { [0.2380952f, 0.3333333f, 0.4285714f] };
            Check(expected, actual);
        }

        [Fact]
        public void L2_ShouldReturnL2_Success()
        {
            var data = new float[,,]
            {
                {
                    { 1f, 2f, 3f },
                    { 4f, 5f, 6f },
                }
            };
            var actual = NormalizeImpl<float>.Normalize(data, NormalizeMode.Mean | NormalizeMode.L2);
            // X = 6.2249498f;
            var expected = new float[][] { [0.40160966f, 0.5622535f, 0.7228974f] };
            Check(expected, actual);
        }

        [Fact]
        public void MinMaxScalling01_ShouldReturnMinMaxScalling01_Success()
        {
            var data = new float[,,]
            {
                {
                    { 1f, 2f, 3f },
                    { 4f, 5f, 6f },
                }
            };
            var actual = NormalizeImpl<float>.Normalize(data, NormalizeMode.Mean | NormalizeMode.MinMaxScalling01);
            var expected = new float[][] { [0f, 0.5f, 1f] };
            Check(expected, actual);
        }

        [Fact]
        public void MinMaxScallingMean_ShouldReturnMinMaxScallingMean_Success()
        {
            var data = new float[,,]
            {
                {
                    { 1f, 2f, 3f },
                    { 4f, 5f, 6f },
                }
            };
            var actual = NormalizeImpl<float>.Normalize(data, NormalizeMode.Mean | NormalizeMode.MinMaxScallingMean);
            var expected = new float[][] { [-0.5f, 0f, 0.5f] };
            Check(expected, actual);
        }

        [Fact]
        public void ZScore_ShouldReturnZScore_Success()
        {
            var data = new float[,,]
            {
                {
                    { 1f, 2f, 3f },
                    { 4f, 5f, 6f },
                }
            };
            var actual = NormalizeImpl<float>.Normalize(data, NormalizeMode.Mean | NormalizeMode.ZScore);
            // STD = 0.81649659f
            var expected = new float[][] { [-1.22474485f, 0f, 1.22474485f] };
            Check(expected, actual);
        }
    }
}
