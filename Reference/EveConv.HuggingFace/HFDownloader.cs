using EveConv.Abstraction.Diagnostic;
using EveConv.Downloader;
using Microsoft.Extensions.Logging;

namespace EveConv.HuggingFace;

/// <summary>
/// Hugging face file downloader.
/// </summary>
public class HFDownloader
{
    private readonly Config _config;
    private readonly ILogger<HFDownloader> _logger;
    private readonly HttpDownloader _downloader;

    /// <summary>
    /// Create instance with specified <see cref="HttpDownloader"/>.
    /// </summary>
    /// <param name="downloader">The specified downloader.</param>
    /// <param name="config">The specified downloader config.</param>
    /// <param name="loggerFactory">Logger factory.</param>
    public HFDownloader(HttpDownloader downloader, Config config, ILoggerFactory? loggerFactory = null)
    {
        this._config = config;
        this._logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<HFDownloader>();
        this._downloader = downloader;
    }

    /// <summary>
    /// Create instance with default <see cref="HttpDownloader"/>.
    /// </summary>
    /// <param name="config">The specified config.</param>
    /// <param name="loggerFactory">Logger factory.</param>
    /// <remarks>It will create the <see cref="HttpDownloader"/> by <paramref name="config"/>.</remarks>
    public HFDownloader(Config config, ILoggerFactory? loggerFactory = null)
        : this(CreateDownloader(config.AuthorizationToken, config.Proxy, config.ExtraHeaders, loggerFactory),
            config, loggerFactory)
    {
    }

    /// <summary>
    /// Create an http downloader.
    /// </summary>
    /// <param name="authToken">Optional authorization bearer token.</param>
    /// <param name="proxy">Optional download proxy.</param>
    /// <param name="headers">Optional request headers.</param>
    /// <param name="loggerFactory">Logger factory.</param>
    /// <returns>A new <see cref="HttpDownloader"/>.</returns>
    private static HttpDownloader CreateDownloader(
        string? authToken = null,
        string? proxy = null,
        IDictionary<string, string>? headers = null,
        ILoggerFactory? loggerFactory = null)
    {
        var h = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(authToken))
        {
            h.Add("Authorization", $"Bearer {authToken}");
        }
        if (headers?.Count > 0)
        {
            foreach (var (k, v) in headers)
            {
                h.TryAdd(k, v);
            }
        }
        return new HttpDownloader(new HttpConfiguration()
        {
            RequestHeaders = h,
            HttpProxy = proxy,
        }, loggerFactory);
    }

    /// <summary>
    /// Asynchronously download a hugging face file.
    /// </summary>
    /// <param name="urlPath">Download url without endpoint. Not the web view url.</param>
    /// <param name="saveTo">Local file path to save.</param>
    /// <param name="forceDownload">Determine whether force download(override) if <paramref name="saveTo"/> has been exists.</param>
    /// <param name="bufferSize">Size of buffer in bytes. Defaults to 80KB.</param>
    /// <param name="progressEmit">Optional progress event.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A Task.</returns>
    public async Task DownloadFileAsync(string urlPath, string saveTo,
        bool forceDownload = false,
        int bufferSize = 81920,
        EventHandler<ProgressArgs<string>>? progressEmit = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(urlPath);
        ArgumentNullException.ThrowIfNull(saveTo);
        if (File.Exists(saveTo))
        {
            if (!forceDownload)
            {
                return;
            }
            File.Delete(saveTo);
        }
        // TODO: check illegal chars and shouldn't contains endpoint.
        if (!urlPath.StartsWith('/') && !urlPath.StartsWith('\\'))
        {
            urlPath = '/' + urlPath;
        }
        var url = this._config.Endpoint + urlPath;
        try
        {
            var response = await _downloader.DownloadAsync(url, cancellationToken);
            await using var stream = await response.GetStreamAsync();
            var totalSize = StreamLength(stream);
            await using var fileStream = new FileStream(saveTo, FileMode.Create, FileAccess.Write);
            int readSize, totalReadSize = 0;
            var buffer = new Memory<byte>(new byte[bufferSize]);
            var prevEmit = DateTime.Now;
            var emitInterval = TimeSpan.FromSeconds(1);
            while ((readSize = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer[..readSize], cancellationToken);
                totalReadSize += readSize;
                if (DateTime.Now - prevEmit <= emitInterval)
                {
                    continue;
                }
                progressEmit?.Invoke(this, new(totalReadSize, totalSize) { Argument = url });
                prevEmit = DateTime.Now;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to download file from url `{Url}` to path `{Path}`. Deleting partial file.", url,
                saveTo);
            if (File.Exists(saveTo))
            {
                File.Delete(saveTo);
            }
            throw;
        }
        return;

        static long? StreamLength(Stream stream)
        {
            try
            {
                return stream.Length;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Asynchronously download a hugging face file.
    /// </summary>
    /// <param name="organization">Organization name.</param>
    /// <param name="repository">Repository name.</param>
    /// <param name="filePath">Relative path of file based on repository.</param>
    /// <param name="saveTo">Local file path to save.</param>
    /// <param name="forceDownload">Determine whether force download(override) if <paramref name="saveTo"/> has been exists.</param>
    /// <param name="bufferSize">Size of buffer in bytes. Defaults to 80KB.</param>
    /// <param name="progressEmit">Optional progress event.</param>    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A Task.</returns>
    public async Task DownloadFileAsync(string organization, string repository, string filePath, string saveTo,
        bool forceDownload = false,
        int bufferSize = 81920,
        EventHandler<ProgressArgs<(string Organization, string Repository, string FilePath)>>? progressEmit = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organization);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(saveTo);
        if (File.Exists(saveTo))
        {
            if (!forceDownload)
            {
                return;
            }
            File.Delete(saveTo);
        }
        var urlPath = $"/{organization}/{repository}/resolve/main/{filePath}";
        await DownloadFileAsync(urlPath, saveTo, forceDownload, bufferSize,
            progressEmit is null
                ? null
                : (s, e) =>
                    progressEmit(s,
                        new(e.DownloadedSize, e.TotalSize)
                        {
                            Argument = (organization, repository, filePath)
                        }),
            cancellationToken);
    }
}

public readonly struct ProgressArgs<Ex>
{
    public long? TotalSize { get; }
    public long? DownloadedSize { get; }
    public double? Percentage => DownloadedSize / TotalSize;
    public Ex? Argument { get; init; }

    public ProgressArgs(long? downloadedSize, long? totalSize)
    {
        this.DownloadedSize = downloadedSize;
        this.TotalSize = totalSize;
    }
}
