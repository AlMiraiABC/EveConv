using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EveConv.Abstraction.Diagnostic;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EveConv.Cache.InMemory;

/// <summary>
/// In-memory cache implementation using Microsoft.Extensions.Caching.Memory as the underlying storage.
/// </summary>
public partial class InMemoryCache : IDisposable
{
    private readonly MemoryCache _memoryCache;
    private readonly InMemoryConfiguration _configuration;
    private readonly ILogger<InMemoryCache> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryCache"/> class.
    /// </summary>
    /// <param name="memoryCache">The underlying memory cache instance.</param>
    /// <param name="configuration">The cache configuration options.</param>
    /// <param name="loggerFactory">Optional logger factory for diagnostic logging.</param>
    /// <exception cref="ArgumentNullException">Thrown when memoryCache or configuration is null.</exception>
    public InMemoryCache(IOptions<InMemoryConfiguration> configuration, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _memoryCache = new MemoryCache(configuration);
        _configuration = configuration.Value;
        _logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<InMemoryCache>();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("InMemoryCache initialized with SizeLimit: {SizeLimit}, CompactionPercentage: {CompactionPercentage}",
                _configuration.SizeLimit, _configuration.CompactionPercentage);
        }
    }

    /// <summary>
    /// Creates cache entry options with TTL and eviction callbacks.
    /// </summary>
    /// <param name="ttl">The time-to-live for the cache entry.</param>
    /// <returns>Configured MemoryCacheEntryOptions.</returns>
    private static MemoryCacheEntryOptions CreateCacheEntryOptions(TimeSpan? ttl)
    {
        var options = new MemoryCacheEntryOptions
        {
            Priority = CacheItemPriority.Normal
        };

        if (ttl.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = ttl.Value;
        }

        return options;
    }

    /// <summary>
    /// Validates that the provided key is not null or empty.
    /// </summary>
    /// <param name="key">The key to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
    /// <exception cref="ArgumentException">Thrown when key is empty.</exception>
    private static void ValidateKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be empty.", nameof(key));
        }
    }

    /// <summary>
    /// Throws an <see cref="ObjectDisposedException"/> if the cache has been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    private void ThrowIfDisposed()
    {
        if (!_disposed)
        {
            return;
        }
        throw new ObjectDisposedException(nameof(InMemoryCache));
    }

    /// <summary>
    /// Releases all resources used by the InMemoryCache.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _disposed = true;
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation($"{nameof(InMemoryCache)} disposed");
            }
        }
    }
}