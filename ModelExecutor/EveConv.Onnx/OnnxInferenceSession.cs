using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
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

        /// <inheritdoc/>
        /// <param name="context">
        /// <list type="bullet">
        ///   <item><description>RunOptions: <see cref="RunOptions"/> Onnx run options. (default: <see langword="null"/>)</description></item>
        ///   <item><description>ArrayedOutputs: <see cref="bool"/> Determine whether convert output to array and dispose ort values. (default: <see langword="true"/>)</description></item>
        /// </list>
        /// </param>
        public override async Task<OnnxInferenceSessionInformation> ExecuteAsync(IDictionary<string, Array?> input, IDictionary<string, object>? context = null, CancellationToken token = default)
        {
            var runoptions = context is not null
                && context.TryGetValue(nameof(OnnxInferenceSessionInformation.RunOptions), out var v)
                && v is RunOptions opt ? opt : null;
            var arrayedoutputs = context is not null
                && context.TryGetValue("ArrayedOutputs", out var ov)
                && bool.TryParse(ov?.ToString(), out var oarr) ? oarr : true;
            input = new Dictionary<string, Array?>(input, StringComparer.OrdinalIgnoreCase);
            var info = new OnnxInferenceSessionInformation(input, this.Instance, runoptions);
            // dispose manually
            var output = this.Instance.Run(info.InputValues, info.OutputNames, info.RunOptions);
            ArrayedOutputs(info, output, arrayedoutputs);
            return info;
        }

        /// <inheritdoc/>
        /// <param name="context">
        /// <list type="bullet">
        ///   <item><description>RunOptions: <see cref="RunOptions"/> Onnx run options. (default: <see langword="null"/>)</description></item>
        ///   <item><description>ArrayedOutputs: <see cref="bool"/> Determine whether convert output to array and dispose ort values. (default: <see langword="false"/>)</description></item>
        /// </list>
        /// </param>
        public async Task<OnnxInferenceSessionInformation> ExecuteAsync(IEnumerable<NamedOnnxValue> input, IDictionary<string, object>? context = null, CancellationToken token = default)
        {
            var runoptions = context is not null
                && context.TryGetValue(nameof(OnnxInferenceSessionInformation.RunOptions), out var v)
                && v is RunOptions opt
                ? opt : null;
            var arrayedoutputs = context is not null
                && context.TryGetValue("ArrayedOutputs", out var ov)
                && bool.TryParse(ov?.ToString(), out var oarr) ? oarr : false;
            var info = new OnnxInferenceSessionInformation(input, this.Instance, runoptions);
            // dispose manually
            var output = this.Instance.Run(info.InputValues, info.OutputNames, runoptions);
            ArrayedOutputs(info, output, arrayedoutputs);
            return info;
        }

        private static void ArrayedOutputs(OnnxInferenceSessionInformation info, IDisposableReadOnlyCollection<DisposableNamedOnnxValue> output, bool needArrayedOutputs = true)
        {
            if (!needArrayedOutputs)
            {
                info.OutputValues = output;
                return;
            }
            var result = output.ToDictionary(i => i.Name, i =>
            {
                try
                {
                    return OnnxValueExtension.ToArray(i);
                }
                catch (Exception ex)
                {
                    throw new OnnxException($"Failed to create output tensor for '{i.Name}'.", ex);
                }
            });
            info.Outputs = result.ToImmutableDictionary();
            output.Dispose();
        }
    }
}
