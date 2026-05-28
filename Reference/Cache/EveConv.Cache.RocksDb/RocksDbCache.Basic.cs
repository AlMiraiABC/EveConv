using System.Text;
using EveConv.Abstraction.Cache;
using Microsoft.Extensions.Logging;

namespace EveConv.Cache.RocksDb;

/// <summary>
/// Basic cache operations implementation for RocksDbCache.
/// </summary>
public partial class RocksDbCache : IBasicCache<object>
{
    /// <inheritdoc />
    public async Task SetAsync(string key, object value, TimeSpan? ttl = null, CancellationToken token = default)
    {
        ThrowIfDisposed();
        ValidateKey(key);

        var effectiveTtl = ttl ?? _configuration.DefaultTtl;
        var payload = SerializeValue(value);
        var entry = CreateEntry(payload, effectiveTtl);
        var entryBytes = SerializeEntry(entry);
        var keyBytes = Encoding.UTF8.GetBytes(key);

        await WithWriteLockAsync(() =>
        {
            _db.Put(keyBytes, entryBytes);
        }, token);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Set cache item with key: {Key}, TTL: {TTL}", key, effectiveTtl);
        }
    }

    /// <inheritdoc />
    public Task<object?> GetAsync(string key, CancellationToken token = default)
    {
        ThrowIfDisposed();
        ValidateKey(key);

        var keyBytes = Encoding.UTF8.GetBytes(key);
        var entry = TryReadEntry(_db.Get(keyBytes), keyBytes);

        if (entry is null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Get cache item with key: {Key}, Found: false", key);
            }
            return Task.FromResult<object?>(null);
        }

        var result = DeserializeValue(entry.Value.Payload);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Get cache item with key: {Key}, Found: true", key);
        }
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<IEnumerable<string>> ListKeysAsync(CancellationToken token = default)
    {
        ThrowIfDisposed();

        var keys = new List<string>();
        using var iterator = _db.NewIterator();
        iterator.SeekToFirst();

        while (iterator.Valid())
        {
            token.ThrowIfCancellationRequested();
            if (TryReadEntry(iterator.Value()) is not null)
            {
                keys.Add(iterator.StringKey());
            }
            iterator.Next();
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Listed {Count} cache keys", keys.Count);
        }
        return Task.FromResult<IEnumerable<string>>(keys);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string key, CancellationToken token = default)
    {
        ThrowIfDisposed();
        ValidateKey(key);

        var keyBytes = Encoding.UTF8.GetBytes(key);
        var existed = _db.Get(keyBytes) is not null;

        await WithWriteLockAsync(() =>
        {
            _db.Remove(keyBytes);
        }, token);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Delete cache item with key: {Key}, Existed: {Existed}", key, existed);
        }
        return existed;
    }

    /// <inheritdoc />
    public Task<long> CountAsync(CancellationToken token = default)
    {
        ThrowIfDisposed();

        long count = 0;
        using var iterator = _db.NewIterator();
        iterator.SeekToFirst();

        while (iterator.Valid())
        {
            token.ThrowIfCancellationRequested();
            if (TryReadEntry(iterator.Value(), iterator.Key()) is not null)
            {
                count++;
            }
            iterator.Next();
        }

        return Task.FromResult(count);
    }
}
