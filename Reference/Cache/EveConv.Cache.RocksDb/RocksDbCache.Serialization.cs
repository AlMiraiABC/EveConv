using MessagePack;

namespace EveConv.Cache.RocksDb;

public partial class RocksDbCache
{
    /// <summary>
    /// Internal cache entry structure for storing value with optional expiration.
    /// </summary>
    [MessagePackObject(AllowPrivate = true)]
    internal readonly record struct CacheEntry(
        [property: Key(0)] byte[] Payload,
        [property: Key(1)] long? ExpirationTicks
    );

    /// <summary>
    /// Serializes a value to bytes using MessagePack.
    /// </summary>
    private static byte[] SerializeValue(object? value)
    {
        if (value is null)
        {
            return Array.Empty<byte>();
        }
        return MessagePackSerializer.Serialize(value);
    }

    /// <summary>
    /// Deserializes bytes to an object using MessagePack.
    /// </summary>
    private static object? DeserializeValue(byte[] bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }
        return MessagePackSerializer.Deserialize<object>(bytes);
    }

    /// <summary>
    /// Creates a cache entry with the given value and optional TTL.
    /// </summary>
    private static CacheEntry CreateEntry(byte[] payload, TimeSpan? ttl)
    {
        long? expirationTicks = ttl.HasValue
            ? DateTime.UtcNow.Ticks + ttl.Value.Ticks
            : null;
        return new CacheEntry(payload, expirationTicks);
    }

    /// <summary>
    /// Serializes a cache entry to bytes for storage.
    /// </summary>
    private static byte[] SerializeEntry(CacheEntry entry)
    {
        return MessagePackSerializer.Serialize(entry);
    }

    /// <summary>
    /// Deserializes a cache entry from bytes.
    /// </summary>
    private static CacheEntry? DeserializeEntry(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }
        return MessagePackSerializer.Deserialize<CacheEntry>(bytes);
    }

    /// <summary>
    /// Checks if a cache entry has expired.
    /// </summary>
    private static bool IsExpired(CacheEntry entry)
    {
        if (!entry.ExpirationTicks.HasValue)
        {
            return false;
        }
        return DateTime.UtcNow.Ticks > entry.ExpirationTicks.Value;
    }

    /// <summary>
    /// Deserializes raw bytes into a valid, non-expired <see cref="CacheEntry"/>.
    /// Returns null if the data is missing, corrupted, or the entry has expired.
    /// When <paramref name="keyBytes"/> is provided and the entry has expired,
    /// the stale entry is removed from the database.
    /// </summary>
    private CacheEntry? TryReadEntry(byte[]? rawBytes, byte[]? keyBytes = null)
    {
        if (rawBytes is null || rawBytes.Length == 0)
            return null;

        var entry = DeserializeEntry(rawBytes);
        if (entry is null)
            return null;

        if (IsExpired(entry.Value))
        {
            if (keyBytes is not null)
                _db.Remove(keyBytes);
            return null;
        }

        return entry;
    }
}
