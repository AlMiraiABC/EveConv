using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EveConv.Abstraction.Cache
{

    /// <summary>
    /// Exception thrown when cache operations fail due to operational issues.
    /// </summary>
    public class CacheOperationException : Exception
    {
        /// <summary>
        /// Gets the cache operation that failed.
        /// </summary>
        public string? Operation { get; }

        /// <summary>
        /// Gets the cache key involved in the failed operation.
        /// </summary>
        public string? Key { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheOperationException"/> class.
        /// </summary>
        public CacheOperationException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheOperationException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public CacheOperationException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheOperationException"/> class with a specified error message and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public CacheOperationException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheOperationException"/> class with operation details.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="operation">The operation that failed.</param>
        /// <param name="key">The cache key involved in the failed operation.</param>
        public CacheOperationException(string message, string operation, string key) : base(message)
        {
            Operation = operation;
            Key = key;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheOperationException"/> class with operation details and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="operation">The operation that failed.</param>
        /// <param name="key">The cache key involved in the failed operation.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public CacheOperationException(string message, string operation, string key, Exception innerException)
            : base(message, innerException)
        {
            Operation = operation;
            Key = key;
        }
    }
}
