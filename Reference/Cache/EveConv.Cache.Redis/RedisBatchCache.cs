using EveConv.Abstraction.Cache;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace EveConv.Cache.Redis;

/// <summary>
/// Batch cache operations implementation for RedisCache using Redis pipelines.
/// </summary>
public partial class RedisCache : IBatchCache<object>
{
    private const int DefaultBatchSize = 100;
    private const int MaxBatchSize = 1000;

    /// <inheritdoc />
    public async Task BatchSetAsync(IDictionary<string, object> items, TimeSpan? ttl = null, CancellationToken token = default)
    {
        ThrowIfDisposed();
        ValidateBatchItems(items);

        if (items.Count == 0)
        {
            _logger.LogDebug("BatchSetAsync called with empty items dictionary");
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

            _logger.LogDebug("Successfully completed batch set operation for {ItemCount} items", items.Count);
        }
        catch (Exception ex) when (ex is not (ArgumentException or ObjectDisposedException))
        {
            _logger.LogError(ex, "Error in batch set operation for {ItemCount} items", items.Count);
            throw new InvalidOperationException($"Error in batch set operation for {items.Count} items", ex);
        }
    }

    /// <inheritdoc />
    public async Task<IDictionary<string, object?>> BatchGetAsync(IEnumerable<string> keys, CancellationToken token = default)
    {
        ThrowIfDisposed();
        ValidateBatchKeys(keys);

        var keyList = keys.ToList();
        if (keyList.Count == 0)
        {
            _logger.LogDebug("BatchGetAsync called with empty keys collection");
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

            _logger.LogDebug("Successfully completed batch get operation for {KeyCount} keys", keyList.Count);
            return result;
        }
        catch (Exception ex) when (ex is not (ArgumentException or ObjectDisposedException))
        {
            _logger.LogError(ex, "Error in batch get operation for {KeyCount} keys", keyList.Count);
            throw new InvalidOperationException($"Error in batch get operation for {keyList.Count} keys", ex);
        }
    }

    /// <summary>
    /// Processes a chunk of items for batch set operation using Redis pipeline.
    /// </summary>
    /// <param name="items">The items to set in this chunk.</param>
    /// <param name="ttl">The optional time-to-live for all items.</param>
    private async Task ProcessBatchSetChunk(IDictionary<string, object> items, TimeSpan? ttl)
    {
        var batch = Database.CreateBatch();
        var tasks = new List<Task>();

        foreach (var item in items)
        {
            var serializedValue = SerializeValue(item.Value);
            var expiry = ttl?.TotalMilliseconds > 0 ? ttl : null;

            var task = batch.StringSetAsync(item.Key, serializedValue, expiry);
            tasks.Add(task);
        }

        // Execute the batch
        batch.Execute();

        // Wait for all operations to complete
        await Task.WhenAll(tasks);

        // Check for any failures
        var failedCount = 0;
        for (int i = 0; i < tasks.Count; i++)
        {
            var task = (Task<bool>)tasks[i];
            if (!task.Result)
            {
                failedCount++;
                var key = items.ElementAt(i).Key;
                _logger.LogWarning("Failed to set cache value for key in batch: {Key}", key);
            }
        }

        if (failedCount > 0)
        {
            _logger.LogWarning("Batch set operation completed with {FailedCount} failures out of {TotalCount} items",
                failedCount, items.Count);
        }
    }

    /// <summary>
    /// Processes a chunk of keys for batch get operation using Redis pipeline.
    /// </summary>
    /// <param name="keys">The keys to retrieve in this chunk.</param>
    /// <returns>A dictionary containing the retrieved key-value pairs.</returns>
    private async Task<Dictionary<string, object?>> ProcessBatchGetChunk(List<string> keys)
    {
        var batch = Database.CreateBatch();
        var tasks = new List<Task<RedisValue>>();

        foreach (var key in keys)
        {
            var task = batch.StringGetAsync(key);
            tasks.Add(task);
        }

        // Execute the batch
        batch.Execute();

        // Wait for all operations to complete
        await Task.WhenAll(tasks);

        // Process results
        var result = new Dictionary<string, object?>();
        for (int i = 0; i < keys.Count; i++)
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
                    _logger.LogWarning(ex, "Failed to deserialize value for key in batch: {Key}", key);
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
        ArgumentNullException.ThrowIfNull(keys);

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
    /// <returns>An enumerable of chunked dictionaries.</returns>
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
    /// <returns>An enumerable of chunked key lists.</returns>
    private static IEnumerable<List<string>> ChunkKeys(List<string> keys, int chunkSize)
    {
        for (int i = 0; i < keys.Count; i += chunkSize)
        {
            yield return keys.Skip(i).Take(chunkSize).ToList();
        }
    }
}