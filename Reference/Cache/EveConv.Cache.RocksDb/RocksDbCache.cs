using EveConv.Abstraction.Cache;
using EveConv.Abstraction.Diagnostic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RocksDbSharp;
using RocksDbNative = RocksDbSharp.RocksDb;

namespace EveConv.Cache.RocksDb;

/// <summary>
/// RocksDB-based cache implementation providing persistent key-value storage
/// with support for basic, batch, enhanced, and list cache operations.
/// </summary>
public partial class RocksDbCache : IDisposable, ICache
{
    private readonly RocksDbNative _db;
    private readonly RocksDbConfiguration _configuration;
    private readonly ILogger<RocksDbCache> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RocksDbCache"/> class.
    /// </summary>
    /// <param name="configuration">The RocksDB configuration options.</param>
    /// <param name="loggerFactory">Optional logger factory for diagnostic logging.</param>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null.</exception>
    public RocksDbCache(IOptions<RocksDbConfiguration> configuration, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _configuration = configuration.Value;
        _logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<RocksDbCache>();

        var dbPath = Path.GetFullPath(_configuration.DatabasePath);
        var dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }

        var options = new DbOptions()
            .SetCreateIfMissing(_configuration.CreateIfMissing)
            .SetKeepLogFileNum(_configuration.KeepLogFileNum ? 1000UL : 0UL)
            .SetMaxOpenFiles(_configuration.MaxOpenFiles)
            .SetWriteBufferSize(_configuration.WriteBufferSize)
            .SetMaxWriteBufferNumber(_configuration.MaxWriteBufferNumber)
            .SetTargetFileSizeBase(_configuration.TargetFileSizeBase);

        if (_configuration.MaxLogFileSize > 0)
        {
            options.SetMaxLogFileSize(_configuration.MaxLogFileSize);
        }

        _db = RocksDbNative.Open(options, dbPath);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("RocksDbCache initialized with path: {Path}", dbPath);
        }
    }

    /// <summary>
    /// Acquires an exclusive write lock for thread-safe write operations.
    /// </summary>
    private async Task<T> WithWriteLockAsync<T>(Func<T> action, CancellationToken token)
    {
        await _writeLock.WaitAsync(token);
        try
        {
            return action();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Acquires an exclusive write lock for thread-safe write operations.
    /// </summary>
    private async Task WithWriteLockAsync(Action action, CancellationToken token)
    {
        await _writeLock.WaitAsync(token);
        try
        {
            action();
        }
        finally
        {
            _writeLock.Release();
        }
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
        throw new ObjectDisposedException(nameof(RocksDbCache));
    }

    /// <summary>
    /// Releases all resources used by the RocksDbCache.
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
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _writeLock.Dispose();
            _db.Dispose();

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation($"{nameof(RocksDbCache)} disposed");
            }
        }

        _disposed = true;
    }
}
