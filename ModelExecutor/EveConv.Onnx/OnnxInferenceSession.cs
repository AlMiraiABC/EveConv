using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using EveConv.Abstraction.ModelExecutor;
using Microsoft.ML.OnnxRuntime;

namespace EveConv.Onnx
{
    public class OnnxInferenceSession
        : InferenceSession<InferenceSession, IDictionary<string, Array?>, OnnxInferenceSessionInformation>
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

        public override async Task<OnnxInferenceSessionInformation> ExecuteAsync(IDictionary<string, Array?> input, IDictionary<string, object>? context = null, CancellationToken token = default)
        {
            var runoptions = context is not null
                && context.TryGetValue(nameof(OnnxInferenceSessionInformation.RunOptions), out var v)
                && v is RunOptions opt
                ? opt : null;
            input = new Dictionary<string, Array?>(input, StringComparer.OrdinalIgnoreCase);
            var info = new OnnxInferenceSessionInformation(input, this.Instance, runoptions);
            using var output = this.Instance.Run(info.InputNames, info.InputValues, info.OutputNames, info.RunOptions);
            var result = new Dictionary<string, Array?>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in output)
            {
                try
                {
                    result.TryAdd(o.Name, OnnxValueExtension.ToArray(o));
                    o.Dispose();
                }
                catch (Exception ex)
                {
                    throw new OnnxException($"Failed to create output tensor for '{o.Name}'.", ex);
                }
            }
            info.Outputs = result.ToImmutableDictionary();
            return info;
        }
    }

    public class OnnxInferenceSessionInformation : IDisposable
    {
        private bool _disposed = false;

        public RunOptions RunOptions { get; set; } = new();
        public IReadOnlyDictionary<string, Array?> Inputs { get; }
        public IReadOnlyDictionary<string, Array?> Outputs { get; internal set; }
        public IReadOnlyCollection<string> InputNames { get; }
        public IReadOnlyCollection<string> OutputNames { get; }

        internal IReadOnlyCollection<FixedBufferOnnxValue> InputValues { get; }

        internal OnnxInferenceSessionInformation(IDictionary<string, Array?> inputData, InferenceSession session, RunOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(session);
            var metadata = session.InputMetadata;
            var names = new List<string>(metadata.Count);
            var inputs = new Dictionary<string, Array?>(StringComparer.OrdinalIgnoreCase);
            var inputValues = new List<FixedBufferOnnxValue>(metadata.Count);
            foreach (var (k, v) in metadata)
            {
                try
                {
                    var shape = Array.ConvertAll(v.Dimensions, Convert.ToInt64);
                    var data = inputData.TryGetValue(k, out var i)
                        ? i
                        : default;
                    var value = ArrayExtension.ToFixedBufferOnnxValue(data, v.ElementDataType, shape);
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
            Outputs = session.OutputNames
                .Select(i => (i, (Array?)null))
                .ToImmutableDictionary(i => i.i, i => i.Item2, StringComparer.OrdinalIgnoreCase);
            InputNames = session.InputNames;
            InputValues = inputValues;
            OutputNames = session.OutputNames;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            GC.SuppressFinalize(this);
            foreach (var i in InputValues)
            {
                i.Dispose();
            }
            _disposed = true;
        }
    }
}
