using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EveConv.Abstraction.Cache
{
    /// <summary>
    /// Provides fundamental cache operations for single-item management with optional expiration support.
    /// </summary>
    /// <typeparam name="TValue">The type of values stored in the cache.</typeparam>
    public interface ICache<TValue>
    {
        /// <summary>
        /// Stores a value in the cache with the specified key and optional time-to-live.
        /// </summary>
        /// <param name="key">The unique identifier for the cached value. Cannot be null or empty.</param>
        /// <param name="value">The value to store in the cache.</param>
        /// <param name="ttl">Optional time-to-live for the cached item. If null, the item will not expire automatically.</param>
        /// <returns>A task representing the asynchronous set operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or contains invalid characters.</exception>
        Task SetAsync(string key, TValue value, TimeSpan? ttl = null);

        /// <summary>
        /// Retrieves a value from the cache by its key.
        /// </summary>
        /// <param name="key">The unique identifier for the cached value. Cannot be null or empty.</param>
        /// <returns>A task containing the cached value if found, or null if the key does not exist or has expired.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or contains invalid characters.</exception>
        Task<TValue?> GetAsync(string key);

        /// <summary>
        /// Retrieves all available cache keys.
        /// </summary>
        /// <returns>A task containing an enumerable of all cache keys currently stored.</returns>
        Task<IEnumerable<string>> ListKeysAsync();

        /// <summary>
        /// Removes a value from the cache by its key.
        /// </summary>
        /// <param name="key">The unique identifier for the cached value to remove. Cannot be null or empty.</param>
        /// <returns>A task containing true if the key was found and removed, false if the key was not found.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or contains invalid characters.</exception>
        Task<bool> DeleteAsync(string key);
    }
}
