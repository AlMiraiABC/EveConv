using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using Microsoft.ML.OnnxRuntime;

namespace EveConv.Onnx
{

    public class OnnxInferenceSessionInformation : IDisposable
    {
        private bool _disposed = false;

        /// <summary>
        /// Onnx run options.
        /// </summary>
        public RunOptions RunOptions { get; set; } = new();
        /// <summary>
        /// Managed inputs.
        /// </summary>
        public IReadOnlyDictionary<string, Array?> Inputs { get; }
        /// <summary>
        /// Managed outputs.
        /// </summary>
        public IReadOnlyDictionary<string, Array?> Outputs { get; internal set; }
        /// <summary>
        /// Names of onnx inputs.
        /// </summary>
        public IReadOnlyCollection<string> InputNames { get; }
        /// <summary>
        /// Names of onnx outputs.
        /// </summary>
        public IReadOnlyCollection<string> OutputNames { get; }

        /// <summary>
        /// Ort values for inputs.
        /// </summary>
        internal IReadOnlyCollection<NamedOnnxValue> InputValues { get; }
        // Should be IDisposableReadOnlyCollection<DisposableNamedOnnxValue>
        /// <summary>
        /// Ort values for outputs that could be reused without memory copy.
        /// Known as KV-Cache.
        /// </summary>
        /// <remarks>Set to <see cref="InputNames"/> to reuse.</remarks>
        public IReadOnlyCollection<DisposableNamedOnnxValue> OutputValues { get; internal set; }

        /// <summary>
        /// Create from arrays when first inference.
        /// </summary>
        /// <param name="inputData">Input arrays.</param>
        /// <param name="session">Onnx inference session.</param>
        /// <param name="options">Onnx run options.</param>
        /// <exception cref="OnnxException"></exception>
        internal OnnxInferenceSessionInformation(IDictionary<string, Array?> inputData, InferenceSession session, RunOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(session);
            var metadata = session.InputMetadata;
            var inputs = new Dictionary<string, Array?>(StringComparer.OrdinalIgnoreCase);
            var inputValues = new List<NamedOnnxValue>(metadata.Count);
            foreach (var (k, v) in metadata)
            {
                try
                {
                    var shape = Array.ConvertAll(v.Dimensions, Convert.ToInt64);
                    var data = inputData.TryGetValue(k, out var i)
                        ? i
                        : default;
                    for (var ds = 0; ds < shape.Length; ds++)
                    {
                        if (shape[ds] == -1 && data is not null)
                        {
                            shape[ds] = data.GetLength(ds);
                        }
                    }
                    var value = ArrayExtension.ToNamedOnnxValue(data, k, v.ElementDataType, shape);
                    inputValues.Add(value);
                    inputs.TryAdd(k, data);
                }
                catch (Exception ex)
                {
                    throw new OnnxException($"Failed to create input tensor for '{k}'.", ex);
                }
            }
            RunOptions = options ?? new();
            Inputs = inputs.ToImmutableDictionary();
            InputNames = session.InputNames;
            InputValues = inputValues;
            Outputs = ToEmptyDict<Array>(session.OutputNames);
            OutputNames = session.OutputNames;
            OutputValues = [];
        }

        /// <summary>
        /// Create from previous outputs to reuse KV-Cache.
        /// </summary>
        /// <param name="inputData">Previout outputs. <see cref="OutputValues"/></param>
        /// <param name="session">Onnx inference session.</param>
        /// <param name="options">Onnx run options.</param>
        internal OnnxInferenceSessionInformation(IEnumerable<NamedOnnxValue> inputData, InferenceSession session, RunOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(inputData);
            var outputNames = session.OutputNames;
            var metadata = session.InputMetadata;
            RunOptions = options ?? new();
            Inputs = ToEmptyDict<Array>(metadata.Keys);
            InputNames = session.InputNames;
            InputValues = [.. inputData];
            Outputs = ToEmptyDict<Array>(outputNames);
            OutputNames = session.OutputNames;
            OutputValues = [];
        }

        private static ImmutableDictionary<string, V?> ToEmptyDict<V>(IEnumerable<string> keys)
        {
            return keys
                .Select(i => (i, default(V)))
                .ToImmutableDictionary(i => i.i, i => i.Item2, StringComparer.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            GC.SuppressFinalize(this);
            if (OutputValues is IDisposableReadOnlyCollection<DisposableNamedOnnxValue> oc)
            {
                oc.Dispose();
            }
            else if (OutputValues is DisposableNamedOnnxValue[] oa)
            {
                foreach (var o in oa)
                {
                    o.Dispose();
                }
            }
            _disposed = true;
        }
    }
}
