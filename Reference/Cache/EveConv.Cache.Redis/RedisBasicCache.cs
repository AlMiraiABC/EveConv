using EveConv.Abstraction.Cache;
using Microsoft.Extensions.Logging;

namespace EveConv.Cache.Redis;

/// <summary>
/// Basic cache operations implementation for RedisCache.
/// </summary>
public partial class RedisCache : IBasicCache<object>
{
    /// <inheritdoc />
    public async Task SetAsync(string key, object value, TimeSpan? ttl = null, CancellationToken token = default)
    {
        ThrowIfDisposed();
        ValidateKey(key);

        try
        {
            var serializedValue = SerializeValue(value);
            var expiry = ttl?.TotalMilliseconds > 0 ? ttl : null;

            var success = await Database.StringSetAsync(key, serializedValue, expiry);

            if (!success)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Failed to set cache value for key: {Key}", key);
                }
                throw new InvalidOperationException($"Failed to set cache value for key: {key}");
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Successfully set cache value for key: {Key} with TTL: {TTL}", key, ttl);
            }
        }
        catch (Exception ex) when (ex is not (ArgumentException or InvalidOperationException or ObjectDisposedException))
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Error setting cache value for key: {Key}", key);
            }
            throw new InvalidOperationException($"Error setting cache value for key: {key}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<object?> GetAsync(string key, CancellationToken token = default)
    {
        ThrowIfDisposed();
        ValidateKey(key);

        try
        {
            var serializedValue = await Database.StringGetAsync(key);

            if (!serializedValue.HasValue)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Cache miss for key: {Key}", key);
                }
                return null;
            }

            var deserializedValue = DeserializeValue(serializedValue);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Cache hit for key: {Key}", key);
            }

            return deserializedValue;
        }
        catch (Exception ex) when (ex is not (ArgumentException or ObjectDisposedException))
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Error getting cache value for key: {Key}", key);
            }
            throw new InvalidOperationException($"Error getting cache value for key: {key}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> ListKeysAsync(CancellationToken token = default)
    {
        ThrowIfDisposed();

        try
        {
            var keys = new List<string>();
            var server = Connection.GetServer(Connection.GetEndPoints().First());

            // Use SCAN to safely iterate through all keys
            await foreach (var key in server.KeysAsync(pattern: "*"))
            {
                keys.Add(key.ToString());
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Retrieved {KeyCount} keys from cache", keys.Count);
            }
            return keys;
        }
        catch (Exception ex) when (ex is not ObjectDisposedException)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Error listing cache keys");
            }
            throw new InvalidOperationException("Error listing cache keys", ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string key, CancellationToken token = default)
    {
        ThrowIfDisposed();
        ValidateKey(key);

        try
        {
            // Use UNLINK for non-blocking deletion
            var result = await Database.KeyDeleteAsync(key);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                if (result)
                {
                    _logger.LogDebug("Successfully deleted cache key: {Key}", key);
                }
                else
                {
                    _logger.LogDebug("Cache key not found for deletion: {Key}", key);
                }
            }

            return result;
        }
        catch (Exception ex) when (ex is not (ArgumentException or ObjectDisposedException))
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Error deleting cache key: {Key}", key);
            }
            throw new InvalidOperationException($"Error deleting cache key: {key}", ex);
        }
    }

    public async Task<long> CountAsync(CancellationToken token = default)
    {
        ThrowIfDisposed();
        var endpoint = _connectionMultiplexer.Value.GetEndPoints().First();
        var server = _connectionMultiplexer.Value.GetServer(endpoint);
        return await server.DatabaseSizeAsync(Database.Database);
    }
}