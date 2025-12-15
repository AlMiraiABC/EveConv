using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using EveConv.Abstraction;
using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.Downloader;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EveConv.Downloader
{
    public class HttpDownloader : IDownloader
    {
        private readonly ILogger<HttpDownloader> _logger;

        private readonly HttpClient _defaultClient;
        private readonly Dictionary<Regex, HttpClient> _httpClients = [];
        private readonly MemoryCache _urlCache;

        /// <summary>
        /// Create a new instance of <see cref="HttpDownloader"/> with default client factory.
        /// </summary>
        public HttpDownloader(IOptions<HttpConfiguration> options, ILoggerFactory? loggerFactory = null)
            : this(options, CreateHttpClient, loggerFactory)
        {
        }

        /// <summary>
        /// Create a new instance of <see cref="HttpDownloader"/> with specified client factory.
        /// </summary>
        /// <param name="clientFactory">Factory to create http client with specified hosted configuration.</param>
        public HttpDownloader(
            IOptions<HttpConfiguration> options,
            Func<HttpHostedConfiguration, HttpClient> clientFactory,
            ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(clientFactory);
            this._logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<HttpDownloader>();
            if (options.Value.Hosts is not null)
            {
                this._httpClients = options.Value.Hosts
                    .ToDictionary(
                        i => new Regex(i.Key, RegexOptions.IgnoreCase | RegexOptions.Compiled),
                        i => clientFactory(i.Value)
                    );
            }
            this._defaultClient = clientFactory(options.Value);
            this._urlCache = new MemoryCache(options.Value.UrlCache, loggerFactory ?? DefaultLogger.Factory);
        }

        public async Task<StreamableFileContent> DownloadAsync(string filePath, CancellationToken token = default)
        {
            var client = GetHttpClient(filePath, out var uri);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if (this._logger.IsEnabled(LogLevel.Debug))
            {
                this._logger.LogDebug("Got response {status}<{length} for {Url}", response.StatusCode, response.Content.Headers.ContentLength, uri);
            }
            response.EnsureSuccessStatusCode();
            string filename = string.Empty;
            if (response.Content.Headers.ContentDisposition is not null)
            {
                var cd = response.Content.Headers.ContentDisposition;
                if (!string.IsNullOrWhiteSpace(cd.FileNameStar))
                {
                    filename = cd.FileNameStar.Trim('"');
                }
                if (!string.IsNullOrWhiteSpace(cd.FileName))
                {
                    filename = cd.FileName.Trim('"');
                }
            }
            if (string.IsNullOrWhiteSpace(filename))
            {
                filename = Path.GetFileName(uri.LocalPath);
            }
            if (string.IsNullOrWhiteSpace(filename))
            {
                filename = Guid.NewGuid().ToString("N");
            }
            return new(
                filename,
                response.Content.Headers.ContentLength ?? -1,
                async () => await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false),
                response.Content.Headers.ContentType?.ToString(),
                response.Content.Headers.LastModified);
        }

        private HttpClient GetHttpClient(string url, out Uri uri)
        {
            uri = new(url);
            if (this._urlCache.TryGetValue(uri.Host, out var httpClient))
            {
                return (httpClient as HttpClient)!;
            }
            foreach (var (pattern, client) in this._httpClients)
            {
                if (!pattern.IsMatch(uri.Host))
                {
                    continue;
                }
                this._urlCache.Set(uri.Host, client);
                return client;
            }
            return _defaultClient;
        }

        private static HttpClient CreateHttpClient(HttpHostedConfiguration config)
        {
            var handler = new HttpClientHandler();
            if (config.HttpProxy is not null)
            {
                var uri = new UriBuilder()
                {
                    Scheme = Uri.UriSchemeHttp,
                    Host = config.HttpProxy.Host,
                    Port = config.HttpProxy.Port ?? HttpProxyConfiguration.DEFALT_PORT,
                }.Uri;
                var proxy = new WebProxy(uri)
                {
                    BypassList = config.HttpProxy.ByPass,
                };
                if (!string.IsNullOrWhiteSpace(config.HttpProxy.Username))
                {
                    proxy.Credentials = new NetworkCredential(config.HttpProxy.Username, config.HttpProxy.Password);
                    //handler.DefaultProxyCredentials = proxy.Credentials;
                }
                handler.UseProxy = true;
                handler.Proxy = proxy;
            }
            var client = new HttpClient(handler);
            if (config.RequestHeaders is not null)
            {
                foreach (var (k, v) in config.RequestHeaders)
                {
                    client.DefaultRequestHeaders.Add(k, [v]);
                }
            }
            return client;
        }
    }
}
