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
                    { 7f, 8f, 9f }
                },
                {
                    { 11f, 12f, 13f },
                    { 14f, 15f, 16f },
                    { 17f, 18f, 19f }
                }
            };
            var actual = NormalizeImpl<float>.Normalize(data, NormalizeMode.Max);
            var expected = new float[][] { [3f, 6f, 9f], [13f, 16f, 19f] };
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
                    { 7f, 8f, 9f }
                },
                {
                    { 11f, 12f, 13f },
                    { 14f, 15f, 16f },
                    { 17f, 18f, 19f }
                }
            };
            var actual = NormalizeImpl<float>.Normalize(data, NormalizeMode.Mean);
            var expected = new float[][] { [2f, 5f, 8f], [12f, 15f, 18f] };
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
                    { 7f, 8f, 9f }
                },
                {
                    { 11f, 12f, 13f },
                    { 14f, 15f, 16f },
                    { 17f, 18f, 19f }
                }
            };
            var actual = NormalizeImpl<float>.Normalize(data, NormalizeMode.Sum);
            var expected = new float[][] { [6f, 15f, 24f], [36f, 45f, 54f] };
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
                    { 7f, 8f, 9f }
                },
                {
                    { 11f, 12f, 13f },
                    { 14f, 15f, 16f },
                    { 17f, 18f, 19f }
                }
            };
            var actual = NormalizeImpl<float>.Normalize(data, NormalizeMode.MeanSquareRootTokensLength);
            var expected = new float[][] { [3.4641018f, 8.6602545f, 13.856407f], [20.78461f, 25.980762f, 31.176914f] };
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
                    { 7f, 8f, 9f }
                },
                {
                    { 11f, 12f, 13f },
                    { 14f, 15f, 16f },
                    { 17f, 18f, 19f }
                }
            };
            var actual = NormalizeImpl<float>.Normalize(data, NormalizeMode.Mean | NormalizeMode.L1);
            // X = 15f; X = 45f;
            var expected = new float[][] { [0.1333333f, 0.33333333f, 0.5333333f], [0.2666666f, 0.33333333f, 0.4f] };
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
                    { 7f, 8f, 9f }
                },
                {
                    { 11f, 12f, 13f },
                    { 14f, 15f, 16f },
                    { 17f, 18f, 19f }
                }
            };
            var actual = NormalizeImpl<float>.Normalize(data, NormalizeMode.Mean | NormalizeMode.L2);
            // X = 9.643650760f; X = 26.324893162f;
            var expected = new float[][] { [0.2073903f, 0.5184758f, 0.8295614f], [0.4558423f, 0.5698029f, 0.68376346f] };
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
                    { 7f, 8f, 9f }
                },
                {
                    { 11f, 12f, 13f },
                    { 14f, 15f, 16f },
                    { 17f, 18f, 19f }
                }
            };
            var actual = NormalizeImpl<float>.Normalize(data, NormalizeMode.Mean | NormalizeMode.MinMaxScalling01);
            var expected = new float[][] { [0f, 0.5f, 1f], [0f, 0.5f, 1f] };
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
                    { 7f, 8f, 9f }
                },
                {
                    { 11f, 12f, 13f },
                    { 14f, 15f, 16f },
                    { 17f, 18f, 19f }
                }
            };
            var actual = NormalizeImpl<float>.Normalize(data, NormalizeMode.Mean | NormalizeMode.MinMaxScallingMean);
            var expected = new float[][] { [-0.5f, 0f, 0.5f], [-0.5f, 0f, 0.5f] };
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
                    { 7f, 8f, 9f }
                },
                {
                    { 11f, 12f, 13f },
                    { 14f, 15f, 16f },
                    { 17f, 18f, 19f }
                }
            };
            var actual = NormalizeImpl<float>.Normalize(data, NormalizeMode.Mean | NormalizeMode.ZScore);
            // STD = 2.4494897f; STD = 2.4494897f;
            var expected = new float[][] { [-1.22474487f, 0f, 1.22474487f], [-1.22474487f, 0f, 1.22474487f] };
            Check(expected, actual);
        }
    }
}
