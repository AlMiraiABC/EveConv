using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace EveConv.Onnx
{
    internal static class ArrayExtension
    {
        public static FixedBufferOnnxValue ToFixedBufferOnnxValue<T>(this IEnumerable<T>? flatData, long[] shape)
            where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(flatData);
            ArgumentNullException.ThrowIfNull(shape);

            var dims = Array.ConvertAll(shape, static i => checked((int)i));
            var data = flatData as T[] ?? [.. flatData];
            var dt = new DenseTensor<T>(data, dims);
            return FixedBufferOnnxValue.CreateFromTensor(dt);
        }

        public static FixedBufferOnnxValue ToFixedBufferOnnxValue(this Array? data, TensorElementType elementType, long[] shape)
        {
            ArgumentNullException.ThrowIfNull(shape);
            return elementType switch
            {
                TensorElementType.UInt8 => ToFixedBufferOnnxValue((IEnumerable<byte>?)data, shape),
                TensorElementType.Int8 => ToFixedBufferOnnxValue((IEnumerable<sbyte>?)data, shape),
                TensorElementType.UInt16 => ToFixedBufferOnnxValue((IEnumerable<ushort>?)data, shape),
                TensorElementType.Int16 => ToFixedBufferOnnxValue((IEnumerable<short>?)data, shape),
                TensorElementType.UInt32 => ToFixedBufferOnnxValue((IEnumerable<uint>?)data, shape),
                TensorElementType.Int32 => ToFixedBufferOnnxValue((IEnumerable<int>?)data, shape),
                TensorElementType.UInt64 => ToFixedBufferOnnxValue((IEnumerable<ulong>?)data, shape),
                TensorElementType.Int64 => ToFixedBufferOnnxValue((IEnumerable<long>?)data, shape),
                TensorElementType.Bool => ToFixedBufferOnnxValue((IEnumerable<bool>?)data, shape),
                TensorElementType.Float => ToFixedBufferOnnxValue((IEnumerable<float>?)data, shape),
                TensorElementType.Float16 => ToFixedBufferOnnxValue((IEnumerable<Float16>?)data, shape),
                TensorElementType.BFloat16 => ToFixedBufferOnnxValue((IEnumerable<BFloat16>?)data, shape),
                TensorElementType.Double => ToFixedBufferOnnxValue((IEnumerable<double>?)data, shape),
                _ => throw new NotSupportedException($"Tensor element type '{elementType}' is not supported."),
            };
        }
    }

    internal static class OnnxValueExtension
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
