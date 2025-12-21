using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EveConv.Abstraction.Cache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace EveConv.Cache.InMemory
{
    public partial class InMemoryCache : IListCache<object>
    {
        /// <summary>
        /// Gets or creates a list from the cache.
        /// </summary>
        private LinkedList<object> GetOrCreateList(string key)
        {
            if (_memoryCache.TryGetValue(key, out var cached))
            {
                if (cached is not LinkedList<object> list)
                {
                    throw new CacheOperationException($"Value is not list of key {key}", "Get", key);
                }
                if (list.Count == 0)
                {
                    _logger.LogDebug("Remove empty list value of key {key}", key);
                    _memoryCache.Remove(key);
                    return new();
                }
                return list;
            }
            return new();
        }

        /// <summary>
        /// Stores a list in the cache with optional TTL.
        /// </summary>
        private void StoreList(string key, LinkedList<object> list, TimeSpan? ttl)
        {
            if (list.Count == 0)
            {
                _memoryCache.Remove(key);
            }
            else
            {
                var options = CreateCacheEntryOptions(ttl);
                _memoryCache.Set(key, list, options);
            }
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

            var list = GetOrCreateList(key);
            if (list.Count == 0)
            {
                return Task.FromResult<object?>(null);
            }

            var normalizedIndex = NormalizeIndex(index, list.Count);
            if (normalizedIndex < 0 || normalizedIndex >= list.Count)
            {
                return Task.FromResult<object?>(null);
            }

            var value = list.ElementAt(normalizedIndex);
            return Task.FromResult<object?>(value);
        }

        public Task<int> ListLengthAsync(string key, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ValidateKey(key);

            var list = GetOrCreateList(key);
            return Task.FromResult(list.Count);
        }

        public Task<IList<object>> ListRangeAsync(string key, int start = 0, int stop = -1, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ValidateKey(key);

            var list = GetOrCreateList(key);
            if (list.Count == 0)
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

            var result = list
              .Skip(normalizedStart)
                   .Take(normalizedStop - normalizedStart + 1)
                     .ToList();

            return Task.FromResult<IList<object>>(result);
        }

        public Task<IList<object>> ListLeftPopAsync(string key, int count, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ValidateKey(key);

            if (count < 1)
            {
                throw new ArgumentException("Count must be at least 1.", nameof(count));
            }

            var list = GetOrCreateList(key);
            var result = new List<object>();

            var itemsToRemove = Math.Min(count, list.Count);
            for (int i = 0; i < itemsToRemove; i++)
            {
                result.Add(list.First!.Value);
                list.RemoveFirst();
            }

            StoreList(key, list, null);

            return Task.FromResult<IList<object>>(result);
        }

        public Task<int> ListLeftPushAsync(string key, IEnumerable<object> values, TimeSpan? ttl = null, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ValidateKey(key);
            ArgumentNullException.ThrowIfNull(values);

            var list = GetOrCreateList(key);

            foreach (var value in values)
            {
                list.AddFirst(value);
            }

            StoreList(key, list, ttl);

            return Task.FromResult(list.Count);
        }

        public Task<int> ListRemoveAsync(string key, object value, int count = 0, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ValidateKey(key);

            var list = GetOrCreateList(key);
            if (list.Count == 0)
            {
                return Task.FromResult(0);
            }

            int removed = 0;

            if (count == 0)
            {
                // Remove all occurrences
                var node = list.First;
                while (node != null)
                {
                    var nextNode = node.Next;
                    if (Equals(node.Value, value))
                    {
                        list.Remove(node);
                        removed++;
                    }
                    node = nextNode;
                }
            }
            else if (count > 0)
            {
                // Remove from head to tail
                var node = list.First;
                while (node != null && removed < count)
                {
                    var nextNode = node.Next;
                    if (Equals(node.Value, value))
                    {
                        list.Remove(node);
                        removed++;
                    }
                    node = nextNode;
                }
            }
            else // count < 0
            {
                // Remove from tail to head
                var node = list.Last;
                var absCount = Math.Abs(count);
                while (node != null && removed < absCount)
                {
                    var prevNode = node.Previous;
                    if (Equals(node.Value, value))
                    {
                        list.Remove(node);
                        removed++;
                    }
                    node = prevNode;
                }
            }

            StoreList(key, list, null);

            return Task.FromResult(removed);
        }

        public Task<IList<object>> ListRightPopAsync(string key, int count, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ValidateKey(key);

            if (count < 1)
            {
                throw new ArgumentException("Count must be at least 1.", nameof(count));
            }

            var list = GetOrCreateList(key);
            var result = new List<object>();

            var itemsToRemove = Math.Min(count, list.Count);
            for (int i = 0; i < itemsToRemove; i++)
            {
                result.Add(list.Last!.Value);
                list.RemoveLast();
            }

            StoreList(key, list, null);

            return Task.FromResult<IList<object>>(result);
        }

        public Task<int> ListRightPushAsync(string key, IEnumerable<object> values, TimeSpan? ttl = null, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ValidateKey(key);
            ArgumentNullException.ThrowIfNull(values);

            var list = GetOrCreateList(key);

            foreach (var value in values)
            {
                list.AddLast(value);
            }

            StoreList(key, list, ttl);

            return Task.FromResult(list.Count);
        }

        public Task<bool> ListSetByIndexAsync(string key, int index, object value, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ValidateKey(key);

            var list = GetOrCreateList(key);
            if (list.Count == 0)
            {
                return Task.FromResult(false);
            }

            var normalizedIndex = NormalizeIndex(index, list.Count);
            if (normalizedIndex < 0 || normalizedIndex >= list.Count)
            {
                return Task.FromResult(false);
            }

            var node = list.First;
            for (int i = 0; i < normalizedIndex; i++)
            {
                node = node!.Next;
            }

            if (node != null)
            {
                node.Value = value;
                StoreList(key, list, null);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task ListTrimAsync(string key, int start, int stop, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ValidateKey(key);

            var list = GetOrCreateList(key);
            if (list.Count == 0)
            {
                return Task.CompletedTask;
            }

            var length = list.Count;
            var normalizedStart = NormalizeIndex(start, length);
            var normalizedStop = NormalizeIndex(stop, length);

            // Ensure valid range
            normalizedStart = Math.Max(0, normalizedStart);
            normalizedStop = Math.Min(length - 1, normalizedStop);

            if (normalizedStart > normalizedStop || normalizedStart >= length)
            {
                // Remove all elements
                _memoryCache.Remove(key);
                return Task.CompletedTask;
            }

            // Keep only elements in the range [start, stop]
            var elementsToKeep = list
                .Skip(normalizedStart)
                .Take(normalizedStop - normalizedStart + 1)
                .ToList();

            list.Clear();
            foreach (var element in elementsToKeep)
            {
                list.AddLast(element);
            }

            StoreList(key, list, null);

            return Task.CompletedTask;
        }
    }
}
