namespace EveConv.Cache.Redis;

/// <summary>
/// Exception thrown when Redis connection operations fail.
/// </summary>
public class RedisConnectionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RedisConnectionException"/> class.
    /// </summary>
    public RedisConnectionException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisConnectionException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public RedisConnectionException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisConnectionException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public RedisConnectionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when Redis cache operations fail due to serialization issues.
/// </summary>
public class RedisCacheSerializationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RedisCacheSerializationException"/> class.
    /// </summary>
    public RedisCacheSerializationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisCacheSerializationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public RedisCacheSerializationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisCacheSerializationException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public RedisCacheSerializationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
