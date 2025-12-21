using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EveConv.Abstraction.Cache;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace EveConv.Cache.Redis
{
    public partial class RedisCache : IListCache<object>
    {
        public async Task<object?> ListGetByIndexAsync(string key, int index, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ValidateKey(key);

            try
            {
                var serializedValue = await Database.ListGetByIndexAsync(key, index);

                if (!serializedValue.HasValue)
                {
                    _logger.LogDebug("No value found at index {Index} for key: {Key}", index, key);
                    return null;
                }

                var deserializedValue = DeserializeValue(serializedValue);
                _logger.LogDebug("Successfully retrieved value at index {Index} for key: {Key}", index, key);

                return deserializedValue;
            }
            catch (Exception ex) when (ex is not (ArgumentException or ObjectDisposedException))
            {
                _logger.LogError(ex, "Error getting value by index {Index} for key: {Key}", index, key);
                throw new InvalidOperationException($"Error getting value by index {index} for key: {key}", ex);
            }
        }

        public async Task<int> ListLengthAsync(string key, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ValidateKey(key);

            try
            {
                var length = await Database.ListLengthAsync(key);
                _logger.LogDebug("List length for key {Key}: {Length}", key, length);
                return (int)length;
            }
            catch (Exception ex) when (ex is not (ArgumentException or ObjectDisposedException))
            {
                _logger.LogError(ex, "Error getting list length for key: {Key}", key);
                throw new InvalidOperationException($"Error getting list length for key: {key}", ex);
            }
        }

        public async Task<IList<object>> ListRangeAsync(string key, int start = 0, int stop = -1, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ValidateKey(key);

            try
            {
                var serializedValues = await Database.ListRangeAsync(key, start, stop);

                if (serializedValues.Length == 0)
                {
                    _logger.LogDebug("No values found in range [{Start}, {Stop}] for key: {Key}", start, stop, key);
                    return Array.Empty<object>();
                }

                var result = new List<object>(serializedValues.Length);
                foreach (var serializedValue in serializedValues)
                {
                    var deserializedValue = DeserializeValue(serializedValue);
                    if (deserializedValue != null)
                    {
                        result.Add(deserializedValue);
                    }
                }

                _logger.LogDebug("Successfully retrieved {Count} values in range [{Start}, {Stop}] for key: {Key}",
                    result.Count, start, stop, key);

                return result;
            }
            catch (Exception ex) when (ex is not (ArgumentException or ObjectDisposedException))
            {
                _logger.LogError(ex, "Error getting range [{Start}, {Stop}] for key: {Key}", start, stop, key);
                throw new InvalidOperationException($"Error getting range [{start}, {stop}] for key: {key}", ex);
            }
        }

        public async Task<IList<object>> ListLeftPopAsync(string key, int count, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ValidateKey(key);

            if (count < 1)
            {
                throw new ArgumentException("Count must be at least 1.", nameof(count));
            }

            try
            {
                var result = new List<object>(count);

                // Redis LPOP with count is supported in Redis 6.2+
                // For compatibility, we'll pop one at a time
                for (int i = 0; i < count; i++)
                {
                    var serializedValue = await Database.ListLeftPopAsync(key);
                    if (!serializedValue.HasValue)
                    {
                        break; // List is empty
                    }

                    var deserializedValue = DeserializeValue(serializedValue);
                    if (deserializedValue != null)
                    {
                        result.Add(deserializedValue);
                    }
                }

                _logger.LogDebug("Successfully left-popped {Count} values from key: {Key}", result.Count, key);
                return result;
            }
            catch (Exception ex) when (ex is not (ArgumentException or ObjectDisposedException))
            {
                _logger.LogError(ex, "Error left-popping {Count} values from key: {Key}", count, key);
                throw new InvalidOperationException($"Error left-popping {count} values from key: {key}", ex);
            }
        }

        public async Task<int> ListLeftPushAsync(string key, IEnumerable<object> values, TimeSpan? ttl = null, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ValidateKey(key);
            ArgumentNullException.ThrowIfNull(values);

            var valueList = values.ToList();
            if (valueList.Count == 0)
            {
                _logger.LogDebug("LeftPushAsync called with empty values for key: {Key}", key);
                return await ListLengthAsync(key, token);
            }

            try
            {
                // Serialize all values
                var serializedValues = valueList.Select(v => (RedisValue)SerializeValue(v)).ToArray();

                // Push all values at once
                var length = await Database.ListLeftPushAsync(key, serializedValues);

                // Set TTL if specified
                if (ttl.HasValue && ttl.Value.TotalMilliseconds > 0)
                {
                    await Database.KeyExpireAsync(key, ttl.Value);
                }

                _logger.LogDebug("Successfully left-pushed {Count} values to key: {Key}, new length: {Length}",
                    valueList.Count, key, length);

                return (int)length;
            }
            catch (Exception ex) when (ex is not (ArgumentException or ObjectDisposedException))
            {
                _logger.LogError(ex, "Error left-pushing {Count} values to key: {Key}", valueList.Count, key);
                throw new InvalidOperationException($"Error left-pushing {valueList.Count} values to key: {key}", ex);
            }
        }

        public async Task<int> ListRemoveAsync(string key, object value, int count = 0, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ValidateKey(key);

            try
            {
                var serializedValue = SerializeValue(value);
                var removed = await Database.ListRemoveAsync(key, serializedValue, count);

                _logger.LogDebug("Removed {Count} occurrences of value from key: {Key}", removed, key);
                return (int)removed;
            }
            catch (Exception ex) when (ex is not (ArgumentException or ObjectDisposedException))
            {
                _logger.LogError(ex, "Error removing value from key: {Key}", key);
                throw new InvalidOperationException($"Error removing value from key: {key}", ex);
            }
        }

        public async Task<IList<object>> ListRightPopAsync(string key, int count, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ValidateKey(key);

            if (count < 1)
            {
                throw new ArgumentException("Count must be at least 1.", nameof(count));
            }

            try
            {
                var result = new List<object>(count);

                // Redis RPOP with count is supported in Redis 6.2+
                // For compatibility, we'll pop one at a time
                for (int i = 0; i < count; i++)
                {
                    var serializedValue = await Database.ListRightPopAsync(key);
                    if (!serializedValue.HasValue)
                    {
                        break; // List is empty
                    }

                    var deserializedValue = DeserializeValue(serializedValue);
                    if (deserializedValue != null)
                    {
                        result.Add(deserializedValue);
                    }
                }

                _logger.LogDebug("Successfully right-popped {Count} values from key: {Key}", result.Count, key);
                return result;
            }
            catch (Exception ex) when (ex is not (ArgumentException or ObjectDisposedException))
            {
                _logger.LogError(ex, "Error right-popping {Count} values from key: {Key}", count, key);
                throw new InvalidOperationException($"Error right-popping {count} values from key: {key}", ex);
            }
        }

        public async Task<int> ListRightPushAsync(string key, IEnumerable<object> values, TimeSpan? ttl = null, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ValidateKey(key);
            ArgumentNullException.ThrowIfNull(values);

            var valueList = values.ToList();
            if (valueList.Count == 0)
            {
                _logger.LogDebug("RightPushAsync called with empty values for key: {Key}", key);
                return await ListLengthAsync(key, token);
            }

            try
            {
                // Serialize all values
                var serializedValues = valueList.Select(v => (RedisValue)SerializeValue(v)).ToArray();

                // Push all values at once
                var length = await Database.ListRightPushAsync(key, serializedValues);

                // Set TTL if specified
                if (ttl.HasValue && ttl.Value.TotalMilliseconds > 0)
                {
                    await Database.KeyExpireAsync(key, ttl.Value);
                }

                _logger.LogDebug("Successfully right-pushed {Count} values to key: {Key}, new length: {Length}",
                    valueList.Count, key, length);

                return (int)length;
            }
            catch (Exception ex) when (ex is not (ArgumentException or ObjectDisposedException))
            {
                _logger.LogError(ex, "Error right-pushing {Count} values to key: {Key}", valueList.Count, key);
                throw new InvalidOperationException($"Error right-pushing {valueList.Count} values to key: {key}", ex);
            }
        }

        public async Task<bool> ListSetByIndexAsync(string key, int index, object value, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ValidateKey(key);

            try
            {
                var serializedValue = SerializeValue(value);
                await Database.ListSetByIndexAsync(key, index, serializedValue);

                _logger.LogDebug("Successfully set value at index {Index} for key: {Key}", index, key);
                return true;
            }
            catch (RedisServerException ex) when (ex.Message.Contains("ERR index out of range") ||
                 ex.Message.Contains("no such key"))
            {
                _logger.LogDebug("Failed to set value at index {Index} for key: {Key} - index out of range or key doesn't exist",
                    index, key);
                return false;
            }
            catch (Exception ex) when (ex is not (ArgumentException or ObjectDisposedException))
            {
                _logger.LogError(ex, "Error setting value at index {Index} for key: {Key}", index, key);
                throw new InvalidOperationException($"Error setting value at index {index} for key: {key}", ex);
            }
        }

        public async Task ListTrimAsync(string key, int start, int stop, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ValidateKey(key);

            try
            {
                await Database.ListTrimAsync(key, start, stop);
                _logger.LogDebug("Successfully trimmed list to range [{Start}, {Stop}] for key: {Key}", start, stop, key);
            }
            catch (Exception ex) when (ex is not (ArgumentException or ObjectDisposedException))
            {
                _logger.LogError(ex, "Error trimming list to range [{Start}, {Stop}] for key: {Key}", start, stop, key);
                throw new InvalidOperationException($"Error trimming list to range [{start}, {stop}] for key: {key}", ex);
            }
        }
    }
}
