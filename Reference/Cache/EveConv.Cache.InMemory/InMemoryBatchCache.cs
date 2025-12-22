using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EveConv.Abstraction.Cache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace EveConv.Cache.InMemory;

/// <summary>
/// Batch cache operations implementation for InMemoryCache.
/// </summary>
public partial class InMemoryCache : IBatchCache<object>
{
    /// <summary>
    /// Stores multiple key-value pairs in the cache with optional time-to-live applied to all items.
    /// </summary>
    /// <param name="items">A dictionary containing the key-value pairs to store. Keys cannot be null or empty.</param>
    /// <param name="ttl">Optional time-to-live for all cached items. If null, uses DefaultTtl from configuration.</param>
    /// <returns>A task representing the asynchronous batch set operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when items dictionary is null or contains null keys.</exception>
    /// <exception cref="ArgumentException">Thrown when items dictionary contains empty keys.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    public Task BatchSetAsync(IDictionary<string, object> items, TimeSpan? ttl = null, CancellationToken token = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(items);

        var effectiveTtl = ttl ?? _configuration.DefaultTtl;

        // Validate all keys first
        foreach (var key in items.Keys)
        {
            ValidateKey(key);
        }

        // Set all items with the same TTL
        foreach (var kvp in items)
        {
            var options = CreateCacheEntryOptions(effectiveTtl);
            _memoryCache.Set(kvp.Key, kvp.Value, options);
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Batch set {Count} cache items with TTL: {TTL}", items.Count, effectiveTtl);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Retrieves multiple values from the cache by their keys in a single operation.
    /// </summary>
    /// <param name="keys">An enumerable of keys to retrieve. Keys cannot be null or empty.</param>
    /// <returns>A task containing a dictionary with the requested keys and their corresponding values. Missing or expired keys will have null values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when keys enumerable is null or contains null keys.</exception>
    /// <exception cref="ArgumentException">Thrown when keys enumerable contains empty keys.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    public Task<IDictionary<string, object?>> BatchGetAsync(IEnumerable<string> keys, CancellationToken token = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(keys);

        var keyList = keys.ToList();
        var result = new Dictionary<string, object?>();

        // Validate all keys first
        foreach (var key in keyList)
        {
            ValidateKey(key);
        }

        // Retrieve all values
        foreach (var key in keyList)
        {
            var value = _memoryCache.Get(key);
            result[key] = value;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Batch get {Count} cache items, Found: {Found}",
                keyList.Count, result.Values.Count(v => v != null));
        }

        return Task.FromResult<IDictionary<string, object?>>(result);
    }
}