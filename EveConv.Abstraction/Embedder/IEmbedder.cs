using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.Embedder
{
    /// <summary>
    /// Generic interface for embedding models.
    /// </summary>
    /// <typeparam name="V">Type of embedded result item.</typeparam>
    /// <typeparam name="I">Type of input.</typeparam>
    public interface IEmbedder<V, I>
        where V : struct
    {
        /// <summary>
        /// Asynchronously generates an embedding for the specified input.
        /// </summary>
        /// <param name="input">The specified input, cannot be <see langword="null"/>.</param>
        /// <param name="context">The additional context for embedding generation, can be <see langword="null"/>.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that contains embedded result.</returns>
        Task<V[,]> EmbeddingAsync(I input, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously batch generate embeddings for the specified inputs.
        /// </summary>
        /// <param name="inputs">A set of input.</param>
        /// <param name="context">The additional context for embedding generation, can be <see langword="null"/>.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that contains embedded results with shape <c>[batch_size, sequence_length]</c>.</returns>
        Task<V[,,]> BatchEmbeddingAsync(I[] inputs, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default);
    }
}
