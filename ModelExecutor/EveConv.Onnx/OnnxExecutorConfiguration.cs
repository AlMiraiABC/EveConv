using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;

namespace EveConv.Onnx
{
    public class OnnxExecutorConfiguration : IOptions<OnnxExecutorConfiguration>
    {
        /// <summary>
        /// Default execution providers.
        /// </summary>
        /// <remarks>Get available eps automatically if not set.</remarks>
        public string[] DefaultEps { get; init; } = [];

        /// <summary>
        /// Default graph optimization level.
        /// </summary>
        public GraphOptimizationLevel DefaultGraphOptimizationLevel { get; init; } = GraphOptimizationLevel.ORT_ENABLE_ALL;

        /// <summary>
        /// Default execution mode.
        /// </summary>
        public ExecutionMode DefaultExecutionMode { get; init; } = ExecutionMode.ORT_SEQUENTIAL;

        /// <summary>
        /// Default folder path to save optimized models.
        /// </summary>
        public string DefaultOptimizedModelSaveFolder { get => string.IsNullOrWhiteSpace(field) ? "./optm" : field; init; }

        /// <summary>
        /// Default file extension of saved optimized models.
        /// </summary>
        public string DefaultOptimizedModelSaveExtension { get => string.IsNullOrWhiteSpace(field) ? ".ort" : field; init; }

        public OnnxExecutorConfiguration Value => this;

        public void Valid()
        {
            Directory.CreateDirectory(DefaultOptimizedModelSaveFolder);
        }
    }
}
