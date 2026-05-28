using System.Text;
using EveConv.Abstraction.Cache;
using Microsoft.Extensions.Logging;
using RocksDbSharp;

namespace EveConv.Cache.RocksDb;

/// <summary>
/// Batch cache operations implementation for RocksDbCache.
/// </summary>
public partial class RocksDbCache : IBatchCache<object>
{
    /// <inheritdoc />
    public async Task BatchSetAsync(IDictionary<string, object> items, TimeSpan? ttl = null,
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

        await WithWriteLockAsync(() =>
        {
            using var batch = new WriteBatch();
            foreach (var kvp in items)
            {
                var payload = SerializeValue(kvp.Value);
                var entry = CreateEntry(payload, effectiveTtl);
                var entryBytes = SerializeEntry(entry);
                batch.Put(Encoding.UTF8.GetBytes(kvp.Key), entryBytes);
            }
            _db.Write(batch);
        }, token);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Batch set {Count} cache items with TTL: {TTL}", items.Count, effectiveTtl);
        }
    }

    /// <inheritdoc />
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

        // Use MultiGet for efficiency
        var keyArrays = keyList.Select(k => System.Text.Encoding.UTF8.GetBytes(k)).ToArray();
        var rawEntries = _db.MultiGet(keyArrays);

        for (int i = 0; i < keyList.Count; i++)
        {
            var rawBytes = rawEntries[i].Value;
            if (rawBytes is null || rawBytes.Length == 0)
            {
                result[keyList[i]] = null;
                continue;
            }

            var entry = DeserializeEntry(rawBytes);
            if (entry is null || IsExpired(entry.Value))
            {
                result[keyList[i]] = null;
            }
            else
            {
                result[keyList[i]] = DeserializeValue(entry.Value.Payload);
            }
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Batch get {Count} cache items, Found: {Found}",
                keyList.Count, result.Values.Count(v => v != null));
        }

        return Task.FromResult<IDictionary<string, object?>>(result);
    }

    public async Task<long> BatchRemoveAsync(IEnumerable<string> keys, CancellationToken token = default)
    {
        ThrowIfDisposed();
        if (keys is null)
        {
            return 0;
        }
        return await WithWriteLockAsync(() =>
        {
            var c = 0L;
            using var batch = new WriteBatch();
            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }
                batch.Delete(Encoding.UTF8.GetBytes(key));
                c++;
            }
            _db.Write(batch);
            return c;
        }, token);
    }
}
