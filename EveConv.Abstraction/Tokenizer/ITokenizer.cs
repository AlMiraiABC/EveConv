using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.Tokenizer
{
    public interface ITokenizer
    {
    }

    /// <summary>
    /// Generic interface for tokenizer models.
    /// </summary>
    /// <typeparam name="V"></typeparam>
    /// <typeparam name="I"></typeparam>
    public interface ITokenizer<V, I>
    {
        /// <summary>
        /// Asynchronously tokenizes the specified input value.
        /// </summary>
        /// <param name="input">The specified input.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that contains tokenize result.</returns>
        Task<V[]> TokenizeAsync(I input, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously tokenizes the specified batch of input values.
        /// </summary>
        /// <param name="inputs">The specified inputs.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<V[][]> TokenizeBatchAsync(I[] inputs, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default);
    }
}
