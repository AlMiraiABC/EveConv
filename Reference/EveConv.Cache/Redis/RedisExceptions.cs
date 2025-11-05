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

/// <summary>
/// Exception thrown when Redis cache operations fail due to operational issues.
/// </summary>
public class RedisCacheOperationException : Exception
{
    /// <summary>
    /// Gets the Redis operation that failed.
    /// </summary>
    public string? Operation { get; }

    /// <summary>
    /// Gets the cache key involved in the failed operation.
    /// </summary>
    public string? Key { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisCacheOperationException"/> class.
    /// </summary>
    public RedisCacheOperationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisCacheOperationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public RedisCacheOperationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisCacheOperationException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public RedisCacheOperationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisCacheOperationException"/> class with operation details.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="operation">The Redis operation that failed.</param>
    /// <param name="key">The cache key involved in the failed operation.</param>
    public RedisCacheOperationException(string message, string operation, string key) : base(message)
    {
        Operation = operation;
        Key = key;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisCacheOperationException"/> class with operation details and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="operation">The Redis operation that failed.</param>
    /// <param name="key">The cache key involved in the failed operation.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public RedisCacheOperationException(string message, string operation, string key, Exception innerException) 
        : base(message, innerException)
    {
        Operation = operation;
        Key = key;
    }
}