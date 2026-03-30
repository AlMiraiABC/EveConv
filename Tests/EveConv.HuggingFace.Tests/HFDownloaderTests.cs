namespace EveConv.HuggingFace.Tests;

public class HFDownloaderTests
{
    private readonly Config _config;

    public HFDownloaderTests()
    {
        dotenv.net.DotEnv.Load();
        this._config = new()
        {
            Endpoint = Environment.GetEnvironmentVariable("HF_ENDPOINT") ?? string.Empty,
            AuthorizationToken = Environment.GetEnvironmentVariable("HF_TOKEN") ?? string.Empty,
        };
    }

    [Fact]
    public async Task DownloadFileAsync_Download_Success()
    {
        var downloader = new HFDownloader(_config);
        var saveTo = Path.GetTempFileName();
        File.Delete(saveTo); // remove created temp file
        const string org = "google-bert";
        const string repo = "bert-base-uncased";
        const string path = "coreml/fill-mask/float32_model.mlpackage/Manifest.json";
        await downloader.DownloadFileAsync(org, repo, path, saveTo,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(File.Exists(saveTo));
        Assert.True(new FileInfo(saveTo).Length > 0);
    }

    [Fact]
    public async Task DownloadFileAsync_ForceDownload_Success()
    {
        var downloader = new HFDownloader(_config);
        var saveTo = Path.GetTempFileName();
        // File.Delete(saveTo); // zero-byte temp file
        const string org = "google-bert";
        const string repo = "bert-base-uncased";
        const string path = "coreml/fill-mask/float32_model.mlpackage/Manifest.json";
        await downloader.DownloadFileAsync(org, repo, path, saveTo, true,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(File.Exists(saveTo));
        Assert.True(new FileInfo(saveTo).Length > 0);
    }
}
