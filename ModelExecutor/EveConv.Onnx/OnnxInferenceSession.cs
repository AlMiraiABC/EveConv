using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using EveConv.Abstraction.ModelExecutor;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace EveConv.Onnx
{
    public class OnnxInferenceSession
        : InferenceSession<InferenceSession, OnnxInferenceSessionExecuteParameter, IDisposableReadOnlyCollection<OrtValue>>
    {
        public OnnxInferenceSession(InferenceSession instance) : base(instance)
        {
        }

        public override void Dispose()
        {
            base.Dispose();
            GC.SuppressFinalize(this);
            this.Instance.Dispose();
        }

        public override async Task<IDisposableReadOnlyCollection<OrtValue>> ExecuteAsync(OnnxInferenceSessionExecuteParameter param, IDictionary<string, object>? context = null, CancellationToken token = default)
        {
            return this.Instance.Run(param.RunOptions, param.InputNames, param.InputValues, param.OutputNames);
        }

        protected override async Task<OnnxInferenceSessionExecuteParameter> ProcessInputAsync(ReadOnlyMemory<byte> param, IDictionary<string, object>? context = null, CancellationToken token = default)
        {
            if (param.Length == 0)
            {
                throw new ArgumentException("Input should not be empty.", nameof(param));
            }
            RunOptions? options = null;
            if (context is not null)
            {
                context = new Dictionary<string, object>(context, StringComparer.OrdinalIgnoreCase);
                if (context.TryGetValue("RunOptions", out var v) && v is RunOptions opt)
                {
                    options = opt;
                }
            }
            return new(param, this.Instance, options);
        }

        protected override async Task<ReadOnlyMemory<byte>> ProcessOutputAsync(IDisposableReadOnlyCollection<OrtValue> output, IDictionary<string, object>? context = null, CancellationToken token = default)
        {
            var result = new List<byte>(sizeof(float) * output.Count);
            foreach (var o in output)
            {
                var dt = o.GetTensorTypeAndShape().ElementDataType;
                var value = dt switch
                {
                    TensorElementType.Float => GetOrtValue<float>(o),
                    TensorElementType.UInt8 => GetOrtValue<byte>(o),
                    TensorElementType.Int8 => GetOrtValue<sbyte>(o),
                    TensorElementType.UInt16 => GetOrtValue<ushort>(o),
                    TensorElementType.Int16 => GetOrtValue<short>(o),
                    TensorElementType.Int32 => GetOrtValue<int>(o),
                    TensorElementType.UInt32 => GetOrtValue<uint>(o),
                    TensorElementType.Int64 => GetOrtValue<long>(o),
                    TensorElementType.UInt64 => GetOrtValue<ulong>(o),
                    TensorElementType.Bool => GetOrtValue<bool>(o),
                    TensorElementType.Float16 => GetOrtValue<Float16>(o),
                    TensorElementType.BFloat16 => GetOrtValue<BFloat16>(o),
                    TensorElementType.Double => GetOrtValue<double>(o),
                    _ => throw new NotSupportedException($"Type of {dt} is not supported.")
                };
                result.AddRange(value);
            }
            output.Dispose();
            return new ReadOnlyMemory<byte>([.. result]);

            static ReadOnlySpan<byte> GetOrtValue<T>(OrtValue value)
                where T : unmanaged
            {
                return MemoryMarshal.Cast<T, byte>(value.GetTensorDataAsSpan<T>());
            }
        }
    }

    public class OnnxInferenceSessionExecuteParameter
    {
        public RunOptions RunOptions { get; set; } = new();
        public IReadOnlyCollection<string> InputNames { get; set; } = [];
        public IReadOnlyCollection<string> OutputNames { get; set; } = [];
        public IReadOnlyCollection<OrtValue> InputValues { get; set; } = [];

        public OnnxInferenceSessionExecuteParameter(ReadOnlyMemory<byte> inputData, InferenceSession session, RunOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(session);
            var metadata = session.InputMetadata;
            var names = new List<string>(metadata.Count);
            var values = new List<OrtValue>(metadata.Count);
            foreach (var (k, v) in metadata)
            {
                var shape = Array.ConvertAll(v.Dimensions, Convert.ToInt64);
                var value = CreateOrtValue(v.ElementDataType, inputData, shape);
                names.Add(k);
                values.Add(value);
            }
            RunOptions = options ?? new();
            InputNames = [.. names];
            InputValues = [.. values];
            OutputNames = session.OutputNames;
        }

        private static OrtValue CreateOrtValue(TensorElementType dt, ReadOnlyMemory<byte> data, long[] shape)
        {
            return dt switch
            {
                TensorElementType.Float => InnerCreate<float>(),
                TensorElementType.UInt8 => InnerCreate<byte>(),
                TensorElementType.Int8 => InnerCreate<sbyte>(),
                TensorElementType.UInt16 => InnerCreate<ushort>(),
                TensorElementType.Int16 => InnerCreate<short>(),
                TensorElementType.Int32 => InnerCreate<int>(),
                TensorElementType.UInt32 => InnerCreate<uint>(),
                TensorElementType.Int64 => InnerCreate<long>(),
                TensorElementType.UInt64 => InnerCreate<ulong>(),
                TensorElementType.Bool => InnerCreate<bool>(),
                TensorElementType.Float16 => InnerCreate<Float16>(),
                TensorElementType.BFloat16 => InnerCreate<BFloat16>(),
                TensorElementType.Double => InnerCreate<double>(),
                _ => throw new NotSupportedException($"Type of {dt} is not supported.")
            };

            OrtValue InnerCreate<T>()
                where T : unmanaged
            {
                if (data.Length % Marshal.SizeOf<T>() != 0)
                {
                    throw new ArgumentException($"Malformed data length {data.Length} for type {typeof(T)}");
                }
                return OrtValue.CreateTensorValueFromMemory(MemoryMarshal.Cast<byte, T>(data.Span).ToArray(), shape);
            }
        }
    }
}
