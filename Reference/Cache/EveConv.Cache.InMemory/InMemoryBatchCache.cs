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

    public Task BatchSetAsync(IDictionary<string, object> items, TimeSpan? ttl = null,
        CancellationToken token = default)
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