using EveConv.Abstraction.Cache;
using Microsoft.Extensions.Logging;

namespace EveConv.Cache.Redis;

/// <summary>
/// Basic cache operations implementation for RedisCache.
/// </summary>
public partial class RedisCache : ICache<object>
{
    /// <inheritdoc />
    public async Task SetAsync(string key, object value, TimeSpan? ttl = null)
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
                _logger.LogWarning("Failed to set cache value for key: {Key}", key);
                throw new InvalidOperationException($"Failed to set cache value for key: {key}");
            }

            _logger.LogDebug("Successfully set cache value for key: {Key} with TTL: {TTL}", key, ttl);
        }
        catch (Exception ex) when (ex is not (ArgumentException or InvalidOperationException or ObjectDisposedException))
        {
            _logger.LogError(ex, "Error setting cache value for key: {Key}", key);
            throw new InvalidOperationException($"Error setting cache value for key: {key}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<object?> GetAsync(string key)
    {
        ThrowIfDisposed();
        ValidateKey(key);

        try
        {
            var serializedValue = await Database.StringGetAsync(key);

            if (!serializedValue.HasValue)
            {
                _logger.LogDebug("Cache miss for key: {Key}", key);
                return null;
            }

            var deserializedValue = DeserializeValue(serializedValue);
            _logger.LogDebug("Cache hit for key: {Key}", key);

            return deserializedValue;
        }
        catch (Exception ex) when (ex is not (ArgumentException or ObjectDisposedException))
        {
            _logger.LogError(ex, "Error getting cache value for key: {Key}", key);
            throw new InvalidOperationException($"Error getting cache value for key: {key}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> ListKeysAsync()
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

            _logger.LogDebug("Retrieved {KeyCount} keys from cache", keys.Count);
            return keys;
        }
        catch (Exception ex) when (ex is not ObjectDisposedException)
        {
            _logger.LogError(ex, "Error listing cache keys");
            throw new InvalidOperationException("Error listing cache keys", ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string key)
    {
        ThrowIfDisposed();
        ValidateKey(key);

        try
        {
            // Use UNLINK for non-blocking deletion
            var result = await Database.KeyDeleteAsync(key);

            if (result)
            {
                _logger.LogDebug("Successfully deleted cache key: {Key}", key);
            }
            else
            {
                _logger.LogDebug("Cache key not found for deletion: {Key}", key);
            }

            return result;
        }
        catch (Exception ex) when (ex is not (ArgumentException or ObjectDisposedException))
        {
            _logger.LogError(ex, "Error deleting cache key: {Key}", key);
            throw new InvalidOperationException($"Error deleting cache key: {key}", ex);
        }
    }

    /// <summary>
    /// Validates that a cache key is not null or empty and meets Redis requirements.
    /// </summary>
    /// <param name="key">The key to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
    /// <exception cref="ArgumentException">Thrown when key is empty or contains invalid characters.</exception>
    private static void ValidateKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key cannot be empty or whitespace.", nameof(key));
        }

        // Redis keys have a maximum size of 512MB, but we'll use a more reasonable limit
        if (key.Length > 1024)
        {
            throw new ArgumentException("Cache key cannot exceed 1024 characters.", nameof(key));
        }

        // Check for problematic characters that might cause issues
        if (key.Contains('\0'))
        {
            throw new ArgumentException("Cache key cannot contain null characters.", nameof(key));
        }
    }
}