using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using EveConv.Abstraction;
using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.Downloader;
using EveConv.Cache.InMemory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EveConv.Downloader
{
    /// <summary>
    /// Download file from http or https.
    /// </summary>
    public class HttpDownloader : IDownloader
    {
        private readonly ILogger<HttpDownloader> _logger;

        internal readonly HttpClient _defaultClient;
        internal readonly Dictionary<Regex, HttpClient> _httpClients = [];
        internal readonly InMemoryCache _urlCache;

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
        /// <param name="options">Options to create HTTP downloader.</param>
        /// <param name="clientFactory">Factory to create http client with specified hosted configuration.</param>
        /// <param name="loggerFactory">Factory to create logger.</param>
        public HttpDownloader(
            IOptions<HttpConfiguration> options,
            Func<HttpHostedConfiguration, HttpClient> clientFactory,
            ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(clientFactory);
            // options.Value.Valid(); // not valid, ignore null host config.
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
            this._urlCache = new InMemoryCache(options.Value.UrlCache, loggerFactory);
        }

        public async Task<StreamableFileContent> DownloadAsync(string filePath, CancellationToken token = default)
        {
            var (client, uri) = await GetHttpClientAsync(filePath, token).ConfigureAwait(false);
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
                    if (_logger.IsEnabled(LogLevel.Trace))
                    {
                        _logger.LogTrace("Got filename {filename} from content disposition file*", filename);
                    }
                }
                if (!string.IsNullOrWhiteSpace(cd.FileName))
                {
                    filename = cd.FileName.Trim('"');
                    if (_logger.IsEnabled(LogLevel.Trace))
                    {
                        _logger.LogTrace("Got filename {filename} from content disposition file", filename);
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(filename))
            {
                filename = Path.GetFileName(uri.LocalPath);
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("Got filename {filename} from uri path", filename);
                }
            }
            if (string.IsNullOrWhiteSpace(filename))
            {
                filename = Guid.NewGuid().ToString("N");
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("Generated filename {filename}", filename);
                }
            }
            return new(
                filename,
                response.Content.Headers.ContentLength ?? -1,
                async () => await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false),
                response.Content.Headers.ContentType?.ToString(),
                response.Content.Headers.LastModified);
        }

        private async Task<(HttpClient HttpClient, Uri Uri)> GetHttpClientAsync(string url, CancellationToken token = default)
        {
            var uri = new Uri(url);
            var httpClient = await this._urlCache.GetAsync(uri.Host, token);
            if (httpClient is not null)
            {
                if (httpClient is HttpClient c)
                {
                    if (_logger.IsEnabled(LogLevel.Trace))
                    {
                        _logger.LogTrace("Got cached http client of uri {uri}", uri);
                    }
                    return (c, uri);
                }
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Excepted {cachetype} but got {actual} of cache key {key}. Delete and recreate it.", typeof(HttpClient), httpClient.GetType(), uri.Host);
                }
                await this._urlCache.DeleteAsync(uri.Host, token);
            }
            foreach (var (pattern, client) in this._httpClients)
            {
                if (!pattern.IsMatch(uri.Host))
                {
                    continue;
                }
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("Matched http client for uri {uri} with pattern {pattern}", uri, pattern);
                }
                await this._urlCache.SetAsync(uri.Host, client, token: token);
                return (client, uri);
            }
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Using default http client for uri {uri}", uri);
            }
            await this._urlCache.SetAsync(uri.Host, _defaultClient, token: token);
            return (_defaultClient, uri);
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
                    Port = config.HttpProxy.Port ?? HttpProxyConfiguration.DEFAULT_PORT,
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
