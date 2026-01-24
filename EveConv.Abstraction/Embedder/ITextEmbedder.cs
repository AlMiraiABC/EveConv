using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.Embedder
{
    /// <summary>
    /// Generic interface for text embedding models.
    /// </summary>
    /// <typeparam name="V">Type of embedded result item.</typeparam>
    public interface ITextEmbedder<V> : IEmbedder<V, string>
        where V : struct
    {
        // TODO: how to supports dense retrieval, multi-vector retrieval, and sparse retrieval, such as BAAI/BGE-M3.
        // hugging face transformer supports dense retrieval only.
        // flag embedding supports all of above, but developed by BAAI that does not compatible of other models or languages.

        /// <summary>
        /// Asynchronously get tokens for the specified input text.
        /// </summary>
        /// <param name="inputs">The specified input text.</param>
        /// <param name="context">Context parameters.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task contains tokens of this input text.</returns>
        Task<long[]> GetTokensAsync(string input, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously get tokens for the specified input texts.
        /// </summary>
        /// <param name="inputs">The specified input texts.</param>
        /// <param name="context">Context parameters.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task contains tokens of these input texts.</returns>
        Task<long[][]> GetTokensAsync(string[] inputs, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default);
    }
}
