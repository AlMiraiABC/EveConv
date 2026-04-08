using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using EveConv.Cache.InMemory;
using Microsoft.Extensions.Options;

namespace EveConv.Downloader
{
    public class HttpConfiguration : HttpHostedConfiguration, IOptions<HttpConfiguration>
    {
        /// <summary>
        /// Host bound configurations to override default settings.
        /// </summary>
        public IDictionary<string, HttpHostedConfiguration> Hosts { get; init; } = new Dictionary<string, HttpHostedConfiguration>(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// Default proxy configuration for all requests.
        /// </summary>
        public new HttpProxyConfiguration? HttpProxy { get; init; }
        /// <summary>
        /// Default headers to add for all requests.
        /// </summary>
        public new IDictionary<string, string> RequestHeaders { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// Configuration for caching url with proxies.
        /// </summary>
        public InMemoryConfiguration UrlCache { get; init; } = new();

        public HttpConfiguration Value => this;

        internal new void Valid()
        {
            base.Valid();
            ArgumentNullException.ThrowIfNull(UrlCache);
            if (Hosts is not null)
            {
                foreach (var (k, v) in Hosts)
                {
                    if (string.IsNullOrWhiteSpace(k))
                    {
                        throw new ArgumentException($"Host should not be null or whitespace.");
                    }
                    v.Valid();
                }
            }
        }
    }

    public class HttpProxyConfiguration
    {
        public const int DEFAULT_PORT = 80;

        /// <summary>
        /// Domain or IP address without port of proxy.
        /// </summary>
        public string Host { get; init; } = string.Empty;
        /// <summary>
        /// Port number of proxy. Default is <see cref="DEFAULT_PORT"/>.
        /// </summary>
        public int? Port { get; init; }
        /// <summary>
        /// Optional username for proxy authentication if required.
        /// </summary>
        public string? Username { get; init; }
        /// <summary>
        /// Optional password for proxy authentication if required.
        /// </summary>
        public string? Password { get; init; }
        /// <summary>
        /// Optional passes through hosts.
        /// </summary>
        public string[] ByPass { get; init; } = [];

        internal void Valid()
        {
            if (Port.HasValue)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Port.Value);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(Port.Value, ushort.MaxValue);
            }
        }

        [return: NotNullIfNotNull(nameof(url))]
        public static implicit operator HttpProxyConfiguration?(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }
            if (!url.Contains("://"))
            {
                url = "http://" + url;
            }
            var uri = new Uri(url, UriKind.RelativeOrAbsolute);
            var user = uri.UserInfo;
            string? username = null, password = null;
            if (!string.IsNullOrWhiteSpace(user))
            {
                var u = user.Split(':');
                username = u[0];
                if (u.Length > 1)
                {
                    password = u[1];
                }
            }
            return new()
            {
                Host = uri.Host,
                Port = uri.IsDefaultPort ? null : uri.Port,
                Username = username,
                Password = password,
            };
        }
    }

    public class HttpHostedConfiguration
    {
        /// <summary>
        /// Optional proxy configuration for this host requests.
        /// </summary>
        public HttpProxyConfiguration? HttpProxy { get; init; }
        /// <summary>
        /// Optional headers to add for this host requests.
        /// </summary>
        /// <remarks>Such as <c>Authorization</c>, <c>Referer</c>, <c>Cookie</c>, <c>User-Agent</c>, ...</remarks>
        public IDictionary<string, string> RequestHeaders { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal void Valid()
        {
            HttpProxy?.Valid();
            if (RequestHeaders.Any(i => string.IsNullOrWhiteSpace(i.Key)))
            {
                throw new ArgumentException("RequestHeaders key should not be null or whitespace.");
            }
        }
    }
}
