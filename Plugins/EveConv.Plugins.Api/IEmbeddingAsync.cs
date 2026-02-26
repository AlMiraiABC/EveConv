using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.AI;

namespace EveConv.Plugins.Api
{
    public interface IEmbeddingAsync<TInput, TOutput> : IPlugin
    {
        /// <summary>
        /// Asynchronously generate embedding for this input.
        /// </summary>
        /// <param name="input">The specified input.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that contains generated embedding.</returns>
        Task<Embedding<TOutput>> GetEmbeddingAsync(TInput input, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously generate embeddings for these inputs.
        /// </summary>
        /// <param name="input">The specified inputs.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that contains generated embeddings.</returns>
        Task<GeneratedEmbeddings<Embedding<TOutput>>> GetEmbeddingsAsync(IEnumerable<TInput> input, CancellationToken cancellationToken = default);
    }

    public interface ITextEmbeddingAsync : IEmbeddingAsync<string, float>
    {

    }
}
