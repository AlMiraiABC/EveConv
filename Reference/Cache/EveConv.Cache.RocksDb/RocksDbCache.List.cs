using System.Text;
using EveConv.Abstraction.Cache;
using MessagePack;

namespace EveConv.Cache.RocksDb;

/// <summary>
/// List cache operations implementation for RocksDbCache.
/// Lists are stored as serialized collections in RocksDB.
/// </summary>
public partial class RocksDbCache : IListCache<object>
{

    /// <summary>
    /// Tries to get a list from the cache. Returns null if the key does not exist or has expired.
    /// </summary>
    private List<byte[]>? TryGetList(string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var entry = TryReadEntry(_db.Get(keyBytes), keyBytes);

        if (entry is null)
            return null;

        try
        {
            return MessagePackSerializer.Deserialize<List<byte[]>>(entry.Value.Payload);
        }
        catch
        {
            _db.Remove(keyBytes);
            return null;
        }
    }

    /// <summary>
    /// Stores a list in the cache. If <paramref name="ttl"/> is null, preserves any existing TTL
    /// on the entry or falls back to <see cref="RocksDbConfiguration.DefaultTtl"/>.
    /// </summary>
    private void StoreList(string key, List<byte[]>? list, TimeSpan? ttl)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        if (list is null || list.Count == 0)
        {
            _db.Remove(keyBytes);
            return;
        }

        // If no explicit TTL, try to preserve the existing one
        if (!ttl.HasValue)
        {
            var existingEntry = TryReadEntry(_db.Get(keyBytes), keyBytes);
            if (existingEntry?.ExpirationTicks is not null)
            {
                ttl = TimeSpan.FromTicks(existingEntry.Value.ExpirationTicks.Value - DateTime.UtcNow.Ticks);
            }
        }

        ttl ??= _configuration.DefaultTtl;

        var payload = MessagePackSerializer.Serialize(list);
        var entry = CreateEntry(payload, ttl);
        _db.Put(keyBytes, SerializeEntry(entry));
    }

    /// <summary>
    /// Normalizes a negative index to a positive index.
    /// </summary>
    private static int NormalizeIndex(int index, int length)
    {
        return index < 0 ? length + index : index;
    }

    public Task<object?> ListGetByIndexAsync(string key, int index, CancellationToken token = default)
    {
        ThrowIfDisposed();
        ValidateKey(key);

        var list = TryGetList(key);
        if (list is null || list.Count == 0)
        {
            return Task.FromResult<object?>(null);
        }

        var normalizedIndex = NormalizeIndex(index, list.Count);
        if (normalizedIndex < 0 || normalizedIndex >= list.Count)
        {
            return Task.FromResult<object?>(null);
        }

        var value = DeserializeValue(list[normalizedIndex]);
        return Task.FromResult(value);
    }

    public Task<int> ListLengthAsync(string key, CancellationToken token = default)
    {
        ThrowIfDisposed();
        ValidateKey(key);

        var list = TryGetList(key);
        return Task.FromResult(list?.Count ?? 0);
    }

    public Task<IList<object>> ListRangeAsync(string key, int start = 0, int stop = -1, CancellationToken token = default)
    {
        ThrowIfDisposed();
        ValidateKey(key);

        var list = TryGetList(key);
        if (list is null || list.Count == 0)
        {
            return Task.FromResult<IList<object>>(Array.Empty<object>());
        }

        var length = list.Count;
        var normalizedStart = NormalizeIndex(start, length);
        var normalizedStop = NormalizeIndex(stop, length);

        // Ensure valid range
        normalizedStart = Math.Max(0, normalizedStart);
        normalizedStop = Math.Min(length - 1, normalizedStop);

        if (normalizedStart > normalizedStop || normalizedStart >= length)
        {
            return Task.FromResult<IList<object>>(Array.Empty<object>());
        }

        var result = new List<object>();
        for (int i = normalizedStart; i <= normalizedStop; i++)
        {
            var value = DeserializeValue(list[i]);
            if (value != null)
            {
                result.Add(value);
            }
        }

        return Task.FromResult<IList<object>>(result);
    }

    public async Task<IList<object>> ListLeftPopAsync(string key, int count, CancellationToken token = default)
    {
        ThrowIfDisposed();
        ValidateKey(key);

        if (count < 1)
        {
            throw new ArgumentException("Count must be at least 1.", nameof(count));
        }

        return await WithWriteLockAsync(() =>
        {
            var list = TryGetList(key);
            if (list is null || list.Count == 0)
            {
                return (IList<object>)[];
            }

            var itemsToRemove = Math.Min(count, list.Count);
            var result = new List<object>(itemsToRemove);

            for (int i = 0; i < itemsToRemove; i++)
            {
                var value = DeserializeValue(list[i]);
                if (value != null)
                {
                    result.Add(value);
                }
            }

            list.RemoveRange(0, itemsToRemove);
            StoreList(key, list, null);

            return result;
        }, token);
    }

    public async Task<int> ListLeftPushAsync(string key, IEnumerable<object> values, TimeSpan? ttl = null, CancellationToken token = default)
    {
        ThrowIfDisposed();
        ValidateKey(key);
        ArgumentNullException.ThrowIfNull(values);

        var valueList = values.ToList();
        if (valueList.Count == 0)
        {
            return await ListLengthAsync(key, token);
        }

        return await WithWriteLockAsync(() =>
        {
            var list = TryGetList(key);
            list ??= [];

            // Insert at the beginning (reverse order to maintain sequence)
            for (int i = valueList.Count - 1; i >= 0; i--)
            {
                list.Insert(0, SerializeValue(valueList[i]));
            }

            StoreList(key, list, ttl);
            return list.Count;
        }, token);
    }

    public async Task<IList<object>> ListRightPopAsync(string key, int count, CancellationToken token = default)
    {
        ThrowIfDisposed();
        ValidateKey(key);

        if (count < 1)
        {
            throw new ArgumentException("Count must be at least 1.", nameof(count));
        }

        return await WithWriteLockAsync(() =>
        {
            var list = TryGetList(key);
            if (list is null || list.Count == 0)
            {
                return [];
            }

            var itemsToRemove = Math.Min(count, list.Count);
            var startIndex = list.Count - itemsToRemove;
            var result = new List<object>(itemsToRemove);

            for (int i = startIndex; i < list.Count; i++)
            {
                var value = DeserializeValue(list[i]);
                if (value != null)
                {
                    result.Add(value);
                }
            }

            list.RemoveRange(startIndex, itemsToRemove);
            StoreList(key, list, null);

            return result;
        }, token);
    }

    public async Task<int> ListRightPushAsync(string key, IEnumerable<object> values, TimeSpan? ttl = null, CancellationToken token = default)
    {
        ThrowIfDisposed();
        ValidateKey(key);
        ArgumentNullException.ThrowIfNull(values);

        var valueList = values.ToList();
        if (valueList.Count == 0)
        {
            return await ListLengthAsync(key, token);
        }

        return await WithWriteLockAsync(() =>
        {
            var list = TryGetList(key) ?? [];

            foreach (var value in valueList)
            {
                list.Add(SerializeValue(value));
            }

            StoreList(key, list, ttl);
            return list.Count;
        }, token);
    }

    public async Task<bool> ListSetByIndexAsync(string key, int index, object value, CancellationToken token = default)
    {
        ThrowIfDisposed();
        ValidateKey(key);

        return await WithWriteLockAsync(() =>
        {
            var list = TryGetList(key);
            if (list is null || list.Count == 0)
            {
                return false;
            }

            var normalizedIndex = NormalizeIndex(index, list.Count);
            if (normalizedIndex < 0 || normalizedIndex >= list.Count)
            {
                return false;
            }

            list[normalizedIndex] = SerializeValue(value);
            StoreList(key, list, null);
            return true;
        }, token);
    }

    public async Task ListTrimAsync(string key, int start, int stop, CancellationToken token = default)
    {
        ThrowIfDisposed();
        ValidateKey(key);

        await WithWriteLockAsync(() =>
        {
            var list = TryGetList(key);
            if (list is null || list.Count == 0)
            {
                return;
            }

            var length = list.Count;
            var normalizedStart = NormalizeIndex(start, length);
            var normalizedStop = NormalizeIndex(stop, length);

            normalizedStart = Math.Max(0, normalizedStart);
            normalizedStop = Math.Min(length - 1, normalizedStop);

            if (normalizedStart > normalizedStop || normalizedStart >= length)
            {
                // Remove the list entirely
                StoreList(key, null, null);
                return;
            }

            var trimmedList = list.GetRange(normalizedStart, normalizedStop - normalizedStart + 1);
            StoreList(key, trimmedList, null);
        }, token);
    }

    public async Task<int> ListRemoveAsync(string key, object value, int count = 0, CancellationToken token = default)
    {
        ThrowIfDisposed();
        ValidateKey(key);

        return await WithWriteLockAsync(() =>
        {
            var list = TryGetList(key);
            if (list is null || list.Count == 0)
            {
                return 0;
            }

            var targetBytes = SerializeValue(value);
            int removed;

            if (count == 0)
            {
                // Remove all occurrences
                removed = list.RemoveAll(item => item.AsSpan().SequenceEqual(targetBytes));
            }
            else if (count > 0)
            {
                // Remove from head to tail
                removed = 0;
                for (int i = list.Count - 1; i >= 0 && removed < count; i--)
                {
                    if (list[i].AsSpan().SequenceEqual(targetBytes))
                    {
                        list.RemoveAt(i);
                        removed++;
                    }
                }
            }
            else
            {
                // count < 0: Remove from tail to head
                var absCount = -count;
                removed = 0;
                for (int i = 0; i < list.Count && removed < absCount; i++)
                {
                    if (list[i].AsSpan().SequenceEqual(targetBytes))
                    {
                        list.RemoveAt(i);
                        i--; // Adjust index after removal
                        removed++;
                    }
                }
            }

            StoreList(key, list, null);
            return removed;
        }, token);
    }
}
