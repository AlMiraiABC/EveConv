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
        private readonly HttpConfiguration _config;

        private readonly HttpClient _defaultClient;
        private readonly Dictionary<Regex, HttpClient> _httpClients = [];
        private readonly MemoryCache _urlCache;

        public HttpDownloader(IOptions<HttpConfiguration> options, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(options);
            this._config = options.Value;
            this._logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<HttpDownloader>();
            if (options.Value.Hosts is not null)
            {
                this._httpClients = options.Value.Hosts
                    .ToDictionary(
                        i => new Regex(i.Key, RegexOptions.IgnoreCase | RegexOptions.Compiled),
                        i => CreateHttpClient(i.Value)
                    );
            }
            this._defaultClient = CreateHttpClient(options.Value);
            this._urlCache = new MemoryCache(options.Value.UrlCache, loggerFactory ?? DefaultLogger.Factory);
        }

        public async Task<StreamableFileContent> DownloadAsync(string filePath, CancellationToken token = default)
        {
            var client = GetHttpClient(filePath, out var uri);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return new(
                Path.GetFileName(uri.LocalPath),
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
                this._urlCache.Set(uri, client);
                return client;
            }
            return _defaultClient;
        }

        private static HttpClient CreateHttpClient(HttpHostedConfiguration config)
        {
            var handler = new HttpClientHandler();
            if (config.HttpProxy is not null)
            {
                handler.UseProxy = true;
                handler.DefaultProxyCredentials = new NetworkCredential()
                {
                    Domain = config.HttpProxy.Host,
                    UserName = config.HttpProxy.Username,
                    Password = config.HttpProxy.Password,
                };
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
