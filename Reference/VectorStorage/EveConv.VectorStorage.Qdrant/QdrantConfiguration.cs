using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Options;

namespace EveConv.Storage.Qdrant
{
    public class QdrantConfiguration : IOptions<QdrantConfiguration>
    {
        /// <summary>
        /// Qdrant service endpoint URL.
        /// </summary>
        public Uri? Endpoint { get; init; }

        /// <summary>
        /// Access api key for authentication.
        /// </summary>
        public string? ApiKey { get; init; }

        /// <summary>
        /// Time out for Qdrant service requests.
        /// </summary>
        public TimeSpan Timeout { get; set; } = default;

        public QdrantConfiguration Value => this;

        internal void Valid()
        {
            ArgumentNullException.ThrowIfNull(this.Endpoint);
        }
    }
}
