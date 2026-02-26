using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ML.OnnxRuntime;

namespace EveConv.Plugins.Qwen3Embedding
{
    internal record Config
    {
        public string Model
        {
            get;
            set
            {
                field = (string.IsNullOrWhiteSpace(value) ? "0.6b" : value.Trim()).ToLower();
            }
        } = "0.6b";
        public string Quantized
        {
            get;
            set
            {
                field = (string.IsNullOrWhiteSpace(value) ? "q4f16" : value.Trim()).ToLower();
            }
        } = "q4f16";
        public string OrtOptimizedSaveFolder
        {
            get;
            set
            {
                field = string.IsNullOrWhiteSpace(value) ? "optm" : value.Trim();
            }
        } = "optm";
        public string OrtOptimizedSaveExtension
        {
            get;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    field = ".ort";
                    return;
                }
                if (!value.StartsWith('.'))
                {
                    field = "." + value.Trim();
                }
            }
        } = ".ort";
        public string OrtGraphOptimizationLevel
        {
            get;
            set
            {
                field = string.IsNullOrWhiteSpace(value) ? nameof(GraphOptimizationLevel.ORT_DISABLE_ALL) : value.Trim();
            }
        } = "ORT_ENABLE_ALL";

        public string TokenizerJsonFileName => $"{Model}_tokenizer.json";
        public string ModelFileName => $"{Model}_model_{Quantized}.onnx";

        public string HFToken { get; set; } = string.Empty;
        public string HFEndpoint
        {
            get
            {
                return string.IsNullOrWhiteSpace(field) ? "https://huggingface.co/" : field;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }
                value = value.Trim();
                if (!value.EndsWith('/'))
                {
                    value += "/";
                }
                field = value;
            }
        }
        public string HFProxy { get; set; } = string.Empty;
        public IDictionary<string, string> HFExtraHeaders { get; set; } = new Dictionary<string, string>();
        public string HfDownloadUrl => $"{HFEndpoint}{{0}}/resolve/main/{{1}}";
    }
}
