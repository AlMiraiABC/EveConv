using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace EveConv.Onnx
{
    internal static class ArrayExtension
    {
        /// <summary>
        /// Convert multi-dimensional array to <see cref="NamedOnnxValue"/>
        /// </summary>
        /// <typeparam name="T">Actual type of the elements in the array.</typeparam>
        /// <param name="arr">The multi-dimensional array to convert.</param>
        /// <param name="name">Name of the ONNX value.</param>
        /// <param name="shape">Shape of the tensor.</param>
        /// <returns>A <see cref="NamedOnnxValue"/> of this array.</returns>
        public static NamedOnnxValue ToNamedOnnxValue<T>(this Array? arr, string name, long[] shape)
            where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(arr);
            ArgumentNullException.ThrowIfNull(shape);

            var dims = Array.ConvertAll(shape, static i => checked((int)i));
            var dt = new DenseTensor<T>(Flatten<T>(arr), dims);
            return NamedOnnxValue.CreateFromTensor(name, dt);
        }

        /// <summary>
        /// Flatten multi-dimensional array to one-dimensional array.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arr"></param>
        /// <returns></returns>
        public static T[] Flatten<T>(this Array arr)
        {
            if (arr is null)
            {
                return [];
            }
            var length = arr.Length;
            if (length == 0)
            {
                return [];
            }
            var result = new T[length];
            Buffer.BlockCopy(arr, 0, result, 0, length * Unsafe.SizeOf<T>());
            return result;
        }

        public static NamedOnnxValue ToNamedOnnxValue(this Array? data, string name, TensorElementType elementType, long[] shape)
        {
            ArgumentNullException.ThrowIfNull(shape);
            return elementType switch
            {
                TensorElementType.UInt8 => ToNamedOnnxValue<byte>(data, name, shape),
                TensorElementType.Int8 => ToNamedOnnxValue<sbyte>(data, name, shape),
                TensorElementType.UInt16 => ToNamedOnnxValue<ushort>(data, name, shape),
                TensorElementType.Int16 => ToNamedOnnxValue<short>(data, name, shape),
                TensorElementType.UInt32 => ToNamedOnnxValue<uint>(data, name, shape),
                TensorElementType.Int32 => ToNamedOnnxValue<int>(data, name, shape),
                TensorElementType.UInt64 => ToNamedOnnxValue<ulong>(data, name, shape),
                TensorElementType.Int64 => ToNamedOnnxValue<long>(data, name, shape),
                TensorElementType.Bool => ToNamedOnnxValue<bool>(data, name, shape),
                TensorElementType.Float => ToNamedOnnxValue<float>(data, name, shape),
                TensorElementType.Float16 => ToNamedOnnxValue<Float16>(data, name, shape),
                TensorElementType.BFloat16 => ToNamedOnnxValue<BFloat16>(data, name, shape),
                TensorElementType.Double => ToNamedOnnxValue<double>(data, name, shape),
                _ => throw new NotSupportedException($"Tensor element type '{elementType}' is not supported."),
            };
        }
    }

    public static class OnnxValueExtension
    {
        public static Array? ToArray(NamedOnnxValue? value)
        {
            if (value?.Value is null)
            {
                return null;
            }
            if (value.Value is Array varr)
            {
                return varr;
            }
            if (value.Value is TensorBase vts)
            {
                var info = vts.GetTypeInfo() ?? throw new NotSupportedException("Cannot get tensor type");
                return info.ElementType switch
                {
                    TensorElementType.Float => InnerCreate<float>(),
                    TensorElementType.UInt8 => InnerCreate<byte>(),
                    TensorElementType.Int8 => InnerCreate<sbyte>(),
                    TensorElementType.UInt16 => InnerCreate<ushort>(),
                    TensorElementType.Int16 => InnerCreate<short>(),
                    TensorElementType.Int32 => InnerCreate<int>(),
                    TensorElementType.Int64 => InnerCreate<long>(),
                    TensorElementType.Bool => InnerCreate<bool>(),
                    TensorElementType.Float16 => InnerCreate<Float16>(),
                    TensorElementType.BFloat16 => InnerCreate<BFloat16>(),
                    TensorElementType.Double => InnerCreate<double>(),
                    TensorElementType.UInt32 => InnerCreate<uint>(),
                    TensorElementType.UInt64 => InnerCreate<ulong>(),
                    _ => throw new NotSupportedException($"Tensor type of {info.ElementType} is not supported.")
                };

                Array InnerCreate<T>()
                    where T : unmanaged
                {
                    if (vts is DenseTensor<T> dt)
                    {
                        return ToArray(dt);
                    }
                    throw new NotSupportedException($"Tensor type of {vts.GetType()} is not supported.");
                }
            }
            for (var vt = value.Value.GetType().BaseType; vt is not null; vt = vt.BaseType)
            {
                if (vt.IsGenericType && vt.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    return value.AsEnumerable<int>().ToArray();
                }
            }
            throw new NotSupportedException($"Cannot get value type");
        }

        public static Array ToArray<T>(DenseTensor<T> dt)
            where T : unmanaged
        {
            var dims = dt.Dimensions;
            if (dims.Length == 0)
            {
                return new[] { dt.GetValue(0) };
            }
            var lengths = new int[dims.Length];
            for (var i = 0; i < dims.Length; i++)
            {
                lengths[i] = dims[i];
            }
            var result = Array.CreateInstance(typeof(T), lengths);
            if (result.Rank == 1)
            {
                dt.Buffer.Span.CopyTo((T[])result);
                return result;
            }
            var span = dt.Buffer.Span;
            var indices = new int[result.Rank];
            for (var flat = 0; flat < span.Length; flat++)
            {
                var rem = flat;
                for (var d = result.Rank - 1; d >= 0; d--)
                {
                    var len = lengths[d];
                    indices[d] = rem % len;
                    rem /= len;
                }
                result.SetValue(span[flat], indices);
            }
            return result;
        }
    }
}
