using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.AI;

namespace EveConv.Plugins.Api
{
    /// <summary>
    /// Interface for embedding plugin to generate embedding for the specified input.
    /// </summary>
    /// <typeparam name="TInput">Type of input value.</typeparam>
    /// <typeparam name="TOutput">Type of output item.</typeparam>
    public interface IEmbedding<TInput, TOutput> : IPlugin
    {
        /// <summary>
        /// Generate embedding for this input.
        /// </summary>
        /// <param name="input">The specified input.</param>
        /// <returns>Generated embedding.</returns>
        Embedding<TOutput> GetEmbedding(TInput input);

        /// <summary>
        /// Generate embeddings for these inputs.
        /// </summary>
        /// <param name="input">The specified inputs.</param>
        /// <returns>Generated embeddings.</returns>
        GeneratedEmbeddings<Embedding<TOutput>> GetEmbeddings(IEnumerable<TInput> input);
    }

    /// <summary>
    /// Interface for text embedding plugin.
    /// </summary>
    public interface ITextEmbedding : IEmbedding<string, float>
    {

    }
}
