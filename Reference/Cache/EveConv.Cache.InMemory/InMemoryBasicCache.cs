using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EveConv.Abstraction.Cache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace EveConv.Cache.InMemory
{
    public partial class InMemoryCache : IBasicCache<object>
    {

        /// <summary>
        /// Stores a value in the cache with the specified key and optional time-to-live.
        /// </summary>
        /// <param name="key">The unique identifier for the cached value. Cannot be null or empty.</param>
        /// <param name="value">The value to store in the cache.</param>
        /// <param name="ttl">Optional time-to-live for the cached item. If null, uses DefaultTtl from configuration.</param>
        /// <returns>A task representing the asynchronous set operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
        public Task SetAsync(string key, object value, TimeSpan? ttl = null)
        {
            ThrowIfDisposed();
            ValidateKey(key);

            var effectiveTtl = ttl ?? _configuration.DefaultTtl;
            var options = CreateCacheEntryOptions(effectiveTtl);

            _memoryCache.Set(key, value, options);

            _logger.LogDebug("Set cache item with key: {Key}, TTL: {TTL}", key, effectiveTtl);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Retrieves a value from the cache by its key.
        /// </summary>
        /// <param name="key">The unique identifier for the cached value. Cannot be null or empty.</param>
        /// <returns>A task containing the cached value if found, or null if the key does not exist or has expired.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
        public Task<object?> GetAsync(string key)
        {
            ThrowIfDisposed();
            ValidateKey(key);

            var result = _memoryCache.Get(key);

            _logger.LogDebug("Get cache item with key: {Key}, Found: {Found}", key, result != null);
            return Task.FromResult(result);
        }

        /// <summary>
        /// Retrieves all available cache keys.
        /// </summary>
        /// <returns>A task containing an enumerable of all cache keys currently stored.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
        public Task<IEnumerable<string>> ListKeysAsync()
        {
            ThrowIfDisposed();

            var keys = _memoryCache.Keys.Cast<string>().ToList();
            _logger.LogDebug("Listed {Count} cache keys", keys.Count);
            return Task.FromResult<IEnumerable<string>>(keys);
        }

        /// <summary>
        /// Removes a value from the cache by its key.
        /// </summary>
        /// <param name="key">The unique identifier for the cached value to remove. Cannot be null or empty.</param>
        /// <returns>A task containing true if the key was removed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
        public Task<bool> DeleteAsync(string key)
        {
            ThrowIfDisposed();
            ValidateKey(key);
            _memoryCache.Remove(key);

            _logger.LogDebug("Delete cache item with key: {Key}", key);
            return Task.FromResult(true);
        }


    }
}
