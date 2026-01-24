using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Embedder
{
    public static class ArrayExtensions
    {
        /// <summary>
        /// Convert nested array to multi-dimensional array.
        /// </summary>
        /// <typeparam name="V">Type of array item.</typeparam>
        /// <param name="value">The specified array.</param>
        /// <returns>Matrix array.</returns>
        public static V[,] ToMatrix<V>(this V[][] value)
        {
            var d0 = value.Length;
            var d1 = value.Max(i => i.Length);
            var matrix = new V[d0, d1];
            for (var i = 0; i < value.Length; i++)
            {
                var v = value[i];
                for (var j = 0; j < v.Length; j++)
                {
                    matrix[i, j] = v[j];
                }
            }
            return matrix;
        }

        /// <summary>
        /// Convert nested array to multi-dimensional array.
        /// </summary>
        /// <typeparam name="V">Type of array item.</typeparam>
        /// <param name="value">The specified array.</param>
        /// <returns>Matrix array.</returns>
        public static V[,,] ToMatrix<V>(this V[][][] value)
        {
            var d0 = value.Length;
            var d1 = MaxLength(value);
            var d2 = value.Max(MaxLength);
            var matrix = new V[d0, d1, d2];
            for (var i = 0; i < value.Length; i++)
            {
                var v = value[i];
                for (var j = 0; j < v.Length; j++)
                {
                    var w = v[j];
                    for (var k = 0; k < w.Length; k++)
                    {
                        matrix[i, j, k] = w[k];
                    }
                }
            }
            return matrix;

            static int MaxLength(IEnumerable<Array> v)
            {
                return v.Max(i => i.Length);
            }
        }

        /// <summary>
        /// Get the shape of the multi-dimensional array.
        /// </summary>
        /// <param name="arr">The specified array.</param>
        /// <returns>Shape of the array.</returns>
        /// <remarks>The shape represents each dimension's length. For nested array(e.g. <c>V[][]</c>) is one-dimensional.</remarks>
        public static long[] Shape(this Array arr)
        {
            var shape = new long[arr.Rank];
            for (var i = 0; i < arr.Rank; i++)
            {
                shape[i] = arr.GetLength(i);
            }
            return shape;
        }
    }
}
