using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EveConv.Abstraction.Cache
{
    /// <summary>
    /// Enables efficient processing of multiple cache items in single operations for improved performance.
    /// </summary>
    /// <typeparam name="TValue">The type of values stored in the cache.</typeparam>
    public interface IBatchCache<TValue>
    {
        /// <summary>
        /// Stores multiple key-value pairs in the cache with optional time-to-live applied to all items.
        /// </summary>
        /// <param name="items">A dictionary containing the key-value pairs to store. Keys cannot be null or empty.</param>
        /// <param name="ttl">Optional time-to-live for all cached items. If null, the items will not expire automatically.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>A task representing the asynchronous batch set operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when items dictionary is null or contains null keys.</exception>
        /// <exception cref="ArgumentException">Thrown when items dictionary contains empty keys or invalid characters.</exception>
        /// <remarks>
        /// This operation processes all items atomically where possible to maintain data consistency.
        /// The TTL parameter applies uniformly to all items in the batch.
        /// </remarks>
        Task BatchSetAsync(IDictionary<string, TValue> items, TimeSpan? ttl = null, CancellationToken token = default);

        /// <summary>
        /// Retrieves multiple values from the cache by their keys in a single operation.
        /// </summary>
        /// <param name="keys">An enumerator of keys to retrieve. Keys cannot be null or empty.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>A task containing a dictionary with the requested keys and their corresponding values. Missing or expired keys will have null values.</returns>
        /// <exception cref="ArgumentNullException">Thrown when keys enumerable is null or contains null keys.</exception>
        /// <exception cref="ArgumentException">Thrown when keys enumerable contains empty keys or invalid characters.</exception>
        /// <remarks>
        /// The returned dictionary maintains the association between keys and values.
        /// Keys that are not found or have expired will be included in the result with null values.
        /// </remarks>
        Task<IDictionary<string, TValue?>> BatchGetAsync(IEnumerable<string> keys, CancellationToken token = default);
    }
}
