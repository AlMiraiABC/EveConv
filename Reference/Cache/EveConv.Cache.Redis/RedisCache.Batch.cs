using System.Diagnostics.CodeAnalysis;
using EveConv.Abstraction.Cache;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace EveConv.Cache.Redis;

/// <summary>
/// Batch cache operations implementation for RedisCache using Redis pipelines.
/// </summary>
public partial class RedisCache : IBatchCache<object>
{
    // private const int DefaultBatchSize = 100;
    private const int MaxBatchSize = 1000;

    /// <inheritdoc />
    public async Task BatchSetAsync(IDictionary<string, object> items, TimeSpan? ttl = null,
        CancellationToken token = default)
    {
        ThrowIfDisposed();
        ValidateBatchItems(items);

        if (items.Count == 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("BatchSetAsync called with empty items dictionary");
            }
            return;
        }

        try
        {
            // Process in chunks if the batch is too large
            var chunks = ChunkItems(items, MaxBatchSize);

            foreach (var chunk in chunks)
            {
                await ProcessBatchSetChunk(chunk, ttl);
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Successfully completed batch set operation for {ItemCount} items", items.Count);
            }
        }
        catch (Exception ex) when (ex is not (ArgumentException or ObjectDisposedException))
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Error in batch set operation for {ItemCount} items", items.Count);
            }
            throw new InvalidOperationException($"Error in batch set operation for {items.Count} items", ex);
        }
    }

    /// <inheritdoc />
    public async Task<IDictionary<string, object?>> BatchGetAsync(IEnumerable<string> keys,
        CancellationToken token = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(keys);
        var keyList = keys.ToList();
        ValidateBatchKeys(keyList);
        if (keyList.Count == 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("BatchGetAsync called with empty keys collection");
            }
            return new Dictionary<string, object?>();
        }

        try
        {
            var result = new Dictionary<string, object?>();

            // Process in chunks if the batch is too large
            var chunks = ChunkKeys(keyList, MaxBatchSize);

            foreach (var chunk in chunks)
            {
                var chunkResult = await ProcessBatchGetChunk(chunk);
                foreach (var kvp in chunkResult)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Successfully completed batch get operation for {KeyCount} keys", keyList.Count);
            }
            return result;
        }
        catch (Exception ex) when (ex is not (ArgumentException or ObjectDisposedException))
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Error in batch get operation for {KeyCount} keys", keyList.Count);
            }
            throw new InvalidOperationException($"Error in batch get operation for {keyList.Count} keys", ex);
        }
    }

    /// <summary>
    /// Processes a chunk of items for batch set operation using Redis pipeline.
    /// </summary>
    private async Task ProcessBatchSetChunk(IDictionary<string, object> items, TimeSpan? ttl)
    {
        var batch = Database.CreateBatch();
        var tasks = items.Select(i =>
        {
            var serializedValue = SerializeValue(i.Value);
            var expiry = ttl?.TotalMilliseconds > 0 ? ttl : null;
            return batch.StringSetAsync(i.Key, serializedValue, expiry);
        }).ToList();

        // Execute the batch
        batch.Execute();

        // Wait for all operations to complete
        await Task.WhenAll(tasks);

        // Check for any failures
        var failedCount = 0;
        for (var i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];
            if (task.Result)
            {
                continue;
            }
            failedCount++;
            var key = items.ElementAt(i).Key;
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("Failed to set cache value for key in batch: {Key}", key);
            }
        }

        if (failedCount > 0 && _logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning("Batch set operation completed with {FailedCount} failures out of {TotalCount} items",
                failedCount, items.Count);
        }
    }

    /// <summary>
    /// Processes a chunk of keys for batch get operation using Redis pipeline.
    /// </summary>
    private async Task<Dictionary<string, object?>> ProcessBatchGetChunk(List<string> keys)
    {
        var batch = Database.CreateBatch();
        var tasks = keys.Select(key => batch.StringGetAsync(key)).ToList();

        // Execute the batch
        batch.Execute();

        // Wait for all operations to complete
        await Task.WhenAll(tasks);

        // Process results
        var result = new Dictionary<string, object?>();
        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            var redisValue = tasks[i].Result;

            if (redisValue.HasValue)
            {
                try
                {
                    result[key] = DeserializeValue(redisValue);
                }
                catch (Exception ex)
                {
                    if (_logger.IsEnabled(LogLevel.Warning))
                    {
                        _logger.LogWarning(ex, "Failed to deserialize value for key in batch: {Key}", key);
                    }
                    result[key] = null;
                }
            }
            else
            {
                result[key] = null;
            }
        }

        return result;
    }

    /// <summary>
    /// Validates the items dictionary for batch operations.
    /// </summary>
    /// <param name="items">The items to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when items is null.</exception>
    /// <exception cref="ArgumentException">Thrown when items contains invalid keys.</exception>
    private static void ValidateBatchItems(IDictionary<string, object> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        foreach (var key in items.Keys)
        {
            ValidateKey(key);
        }
    }

    /// <summary>
    /// Validates the keys collection for batch operations.
    /// </summary>
    /// <param name="keys">The keys to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when keys is null.</exception>
    /// <exception cref="ArgumentException">Thrown when keys contains invalid values.</exception>
    private static void ValidateBatchKeys(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            ValidateKey(key);
        }
    }

    /// <summary>
    /// Chunks a dictionary of items into smaller batches.
    /// </summary>
    /// <param name="items">The items to chunk.</param>
    /// <param name="chunkSize">The maximum size of each chunk.</param>
    /// <returns>An enumerator of chunked dictionaries.</returns>
    private static IEnumerable<IDictionary<string, object>> ChunkItems(IDictionary<string, object> items, int chunkSize)
    {
        var itemList = items.ToList();
        for (int i = 0; i < itemList.Count; i += chunkSize)
        {
            yield return itemList
                .Skip(i)
                .Take(chunkSize)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }

    /// <summary>
    /// Chunks a list of keys into smaller batches.
    /// </summary>
    /// <param name="keys">The keys to chunk.</param>
    /// <param name="chunkSize">The maximum size of each chunk.</param>
    /// <returns>An enumerator of chunked key lists.</returns>
    private static IEnumerable<List<string>> ChunkKeys(List<string> keys, int chunkSize)
    {
        for (int i = 0; i < keys.Count; i += chunkSize)
        {
            yield return keys.Skip(i).Take(chunkSize).ToList();
        }
    }
}
