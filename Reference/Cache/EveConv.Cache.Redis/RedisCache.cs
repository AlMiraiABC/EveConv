using EveConv.Abstraction.Diagnostic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace EveConv.Cache.Redis;

/// <summary>
/// Redis implementation of cache interfaces providing connection management and basic cache operations.
/// </summary>
public partial class RedisCache : IDisposable
{
    private readonly RedisConfiguration _configuration;
    private readonly ILogger<RedisCache> _logger;
    private readonly Lazy<ConnectionMultiplexer> _connectionMultiplexer;
    private readonly Lazy<IDatabase> _database;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisCache"/> class.
    /// </summary>
    /// <param name="configuration">The Redis configuration options.</param>
    /// <param name="logger">Optional logger for diagnostic logging.</param>
    /// <exception cref="ArgumentNullException">Thrown when configuration or logger is null.</exception>
    /// <exception cref="ArgumentException">Thrown when configuration is invalid.</exception>
    public RedisCache(IOptions<RedisConfiguration> configuration, Logger<RedisCache>? logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _configuration = configuration.Value;
        _logger = logger ?? DefaultLogger<RedisCache>.Instance;

        // Validate configuration
        RedisConfigurationValidator.Validate(_configuration);

        // Initialize lazy connections
        _connectionMultiplexer = new Lazy<ConnectionMultiplexer>(CreateConnection);
        _database = new Lazy<IDatabase>(() => _connectionMultiplexer.Value.GetDatabase());

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("RedisCache initialized with connection string: {ConnectionString}",
                MaskConnectionString(_configuration.ConnectionString));
        }
    }

    /// <summary>
    /// Gets the Redis database instance.
    /// </summary>
    protected IDatabase Database => _database.Value;

    /// <summary>
    /// Gets the Redis connection multiplexer.
    /// </summary>
    protected ConnectionMultiplexer Connection => _connectionMultiplexer.Value;

    /// <summary>
    /// Creates and configures the Redis connection.
    /// </summary>
    /// <returns>A configured ConnectionMultiplexer instance.</returns>
    /// <exception cref="RedisConnectionException">Thrown when connection fails after retries.</exception>
    private ConnectionMultiplexer CreateConnection()
    {
        var configurationOptions = new ConfigurationOptions
        {
            EndPoints = { _configuration.ConnectionString },
            ConnectTimeout = (int)_configuration.ConnectTimeout.TotalMilliseconds,
            CommandMap = CommandMap.Create(new HashSet<string>(), available: false),
            AbortOnConnectFail = _configuration.AbortOnConnectFail,
            ConnectRetry = _configuration.ConnectRetry
        };

        // Enable all commands
        configurationOptions.CommandMap = CommandMap.Default;

        // Add authentication if provided
        if (!string.IsNullOrEmpty(_configuration.Username))
        {
            configurationOptions.User = _configuration.Username;
        }

        if (!string.IsNullOrEmpty(_configuration.Password))
        {
            configurationOptions.Password = _configuration.Password;
        }

        try
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Attempting to connect to Redis server: {ConnectionString}",
                    MaskConnectionString(_configuration.ConnectionString));
            }

            var connection = ConnectionMultiplexer.Connect(configurationOptions);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Successfully connected to Redis server");
            }

            // Subscribe to connection events
            connection.ConnectionFailed += OnConnectionFailed;
            connection.ConnectionRestored += OnConnectionRestored;
            connection.ErrorMessage += OnErrorMessage;

            return connection;
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Failed to connect to Redis server after {RetryCount} attempts",
                    _configuration.ConnectRetry);
            }
            throw new RedisConnectionException("Unable to connect to Redis server", ex);
        }
    }

    /// <summary>
    /// Handles connection failure events.
    /// </summary>
    private void OnConnectionFailed(object? sender, ConnectionFailedEventArgs e)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning("Redis connection failed: {FailureType} - {Exception}",
                e.FailureType, e.Exception?.Message);
        }
    }

    /// <summary>
    /// Handles connection restored events.
    /// </summary>
    private void OnConnectionRestored(object? sender, ConnectionFailedEventArgs e)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Redis connection restored: {FailureType}", e.FailureType);
        }
    }

    /// <summary>
    /// Handles Redis error messages.
    /// </summary>
    private void OnErrorMessage(object? sender, RedisErrorEventArgs e)
    {
        if (_logger.IsEnabled(LogLevel.Error))
        {
            _logger.LogError("Redis error: {Message}", e.Message);
        }
    }

    /// <summary>
    /// Masks sensitive information in connection strings for logging.
    /// </summary>
    /// <param name="connectionString">The connection string to mask.</param>
    /// <returns>A masked version of the connection string.</returns>
    private static string MaskConnectionString(string connectionString)
    {
        // Simple masking - in production, implement more sophisticated masking
        var parts = connectionString.Split(',');
        return string.Join(",", parts.Select(part =>
        {
            var hostPort = part.Split(':');
            return hostPort.Length > 1 ? $"{hostPort[0]}:****" : part;
        }));
    }

    /// <summary>
    /// Releases all resources used by the RedisCache.
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
            if (_connectionMultiplexer.IsValueCreated)
            {
                _connectionMultiplexer.Value.ConnectionFailed -= OnConnectionFailed;
                _connectionMultiplexer.Value.ConnectionRestored -= OnConnectionRestored;
                _connectionMultiplexer.Value.ErrorMessage -= OnErrorMessage;
                _connectionMultiplexer.Value.Dispose();
            }

            _disposed = true;
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("RedisCache disposed");
            }
        }
    }

    /// <summary>
    /// Throws an ObjectDisposedException if the cache has been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    private void ThrowIfDisposed()
    {
        if (!_disposed)
        {
            return;
        }
        throw new ObjectDisposedException(nameof(RedisCache));
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