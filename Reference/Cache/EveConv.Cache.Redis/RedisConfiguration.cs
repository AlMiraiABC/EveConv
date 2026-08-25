using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace EveConv.Cache.Redis;

/// <summary>
/// Configuration options for Redis cache connection and behavior.
/// </summary>
public class RedisConfiguration
{
    /// <summary>
    /// Gets or sets the Redis connection string.
    /// </summary>
    /// <example>localhost:6379</example>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional username for Redis authentication.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the optional password for Redis authentication.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the serialization type to use for cache values.
    /// </summary>
    public SerializationType SerializationType { get; set; } = SerializationType.Json;

    /// <summary>
    /// Gets or sets the number of connection retry attempts.
    /// </summary>
    public int ConnectRetry { get; set; } = 3;

    /// <summary>
    /// Gets or sets the connection timeout duration.
    /// </summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the command timeout duration.
    /// </summary>
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets whether to abort connection on connect failure.
    /// </summary>
    public bool AbortOnConnectFail { get; set; } = false;
}

/// <summary>
/// Defines the available serialization types for cache values.
/// </summary>
public enum SerializationType
{
    /// <summary>
    /// JSON serialization using System.Text.Json.
    /// </summary>
    Json = 0
}

/// <summary>
/// Provides validation methods for Redis configuration settings.
/// </summary>
public static class RedisConfigurationValidator
{
#pragma warning disable SYSLIB1045 // 转换为“GeneratedRegexAttribute”。
    // GeneratedRegex is not required, this validator is a one time setup cost.
    private static readonly Regex ConnectionStringPattern = new(
        @"^([a-zA-Z0-9.-]+)(:\d+)?(\s*,\s*[a-zA-Z0-9.-]+(:\d+)?)*$",
        RegexOptions.Compiled);
#pragma warning restore SYSLIB1045 // 转换为“GeneratedRegexAttribute”。

    /// <summary>
    /// Validates the Redis configuration and throws exceptions for invalid settings.
    /// </summary>
    /// <param name="configuration">The configuration to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null.</exception>
    /// <exception cref="ArgumentException">Thrown when configuration contains invalid values.</exception>
    public static void Validate(RedisConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        ValidateConnectionString(configuration.ConnectionString);
        ValidateTimeouts(configuration);
        ValidateRetrySettings(configuration);
    }

    /// <summary>
    /// Validates the connection string format.
    /// </summary>
    /// <param name="connectionString">The connection string to validate.</param>
    /// <exception cref="ArgumentException">Thrown when connection string is invalid.</exception>
    public static void ValidateConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));
        }

        if (!ConnectionStringPattern.IsMatch(connectionString))
        {
            throw new ArgumentException(
                "Connection string format is invalid. Expected format: 'host:port' or 'host1:port1,host2:port2'.",
                nameof(connectionString));
        }
    }

    /// <summary>
    /// Validates timeout settings.
    /// </summary>
    /// <param name="configuration">The configuration containing timeout settings.</param>
    /// <exception cref="ArgumentException">Thrown when timeout values are invalid.</exception>
    private static void ValidateTimeouts(RedisConfiguration configuration)
    {
        if (configuration.ConnectTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("Connect timeout must be greater than zero.", nameof(configuration.ConnectTimeout));
        }

        if (configuration.CommandTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("Command timeout must be greater than zero.", nameof(configuration.CommandTimeout));
        }

        if (configuration.ConnectTimeout > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentException("Connect timeout cannot exceed 5 minutes.", nameof(configuration.ConnectTimeout));
        }

        if (configuration.CommandTimeout > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentException("Command timeout cannot exceed 5 minutes.", nameof(configuration.CommandTimeout));
        }
    }

    /// <summary>
    /// Validates retry settings.
    /// </summary>
    /// <param name="configuration">The configuration containing retry settings.</param>
    /// <exception cref="ArgumentException">Thrown when retry values are invalid.</exception>
    private static void ValidateRetrySettings(RedisConfiguration configuration)
    {
        if (configuration.ConnectRetry < 0)
        {
            throw new ArgumentException("Connect retry count cannot be negative.", nameof(configuration.ConnectRetry));
        }

        if (configuration.ConnectRetry > 10)
        {
            throw new ArgumentException("Connect retry count cannot exceed 10.", nameof(configuration.ConnectRetry));
        }
    }
}
