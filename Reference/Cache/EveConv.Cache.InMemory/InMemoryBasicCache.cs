using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EveConv.Abstraction.Cache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace EveConv.Cache.InMemory
{
    public partial class InMemoryCache : IBasicCache<object>
    {

        public async Task SetAsync(string key, object value, TimeSpan? ttl = null, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ValidateKey(key);

            var effectiveTtl = ttl ?? _configuration.DefaultTtl;
            var options = CreateCacheEntryOptions(effectiveTtl);

            _memoryCache.Set(key, value, options);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Set cache item with key: {Key}, TTL: {TTL}", key, effectiveTtl);
            }
            return;
        }

        public async Task<object?> GetAsync(string key, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ValidateKey(key);

            var result = _memoryCache.Get(key);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Get cache item with key: {Key}, Found: {Found}", key, result != null);
            }
            return result;
        }

        public async Task<IEnumerable<string>> ListKeysAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();

            var keys = _memoryCache.Keys.Cast<string>().ToList();
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Listed {Count} cache keys", keys.Count);
            }
            return keys;
        }

        public async Task<bool> DeleteAsync(string key, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ValidateKey(key);
            _memoryCache.Remove(key);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Delete cache item with key: {Key}", key);
            }
            return true;
        }

        public async Task<long> CountAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            return _memoryCache.Count;
        }
    }
}
