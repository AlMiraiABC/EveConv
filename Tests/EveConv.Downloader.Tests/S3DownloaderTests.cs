
using Amazon.S3;
using EveConv.S3Helper;
using Testcontainers.Minio;

namespace EveConv.Downloader.Tests
{
    public class S3DownloaderTests : IClassFixture<MinioContainerFixture>, IAsyncLifetime
    {
        private readonly MinioContainer container;
        private readonly S3Downloader downloader;

        private AmazonS3Client _client = null!;

        private static CancellationToken CurrentCT => TestContext.Current.CancellationToken;

        public S3DownloaderTests(MinioContainerFixture fixture)
        {
            this.container = fixture.Container;
            var config = new S3Configuration()
            {
                AccessKey = container.GetAccessKey(),
                SecretKey = container.GetSecretKey(),
                Endpoint = container.GetConnectionString(),
            };
            this.downloader = new(config);
        }

        public async ValueTask InitializeAsync()
        {
            // test connection.
            this._client = new AmazonS3Client(
               container.GetAccessKey(),
               container.GetSecretKey(),
               new AmazonS3Config()
               {
                   ServiceURL = container.GetConnectionString(),
               });
            var response = await _client.ListBucketsAsync(CurrentCT);
            Console.WriteLine("Got {0} buckets", response.Buckets?.Count ?? 0);
        }

        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            return;
        }

        [Fact]
        public async Task DownloadAsync_Exist_Success()
        {
            var bucketName = await CreateBucketAsync(CurrentCT);
            var key = $"{Guid.NewGuid()}.txt";
            var content = Guid.NewGuid().ToString();
            var contentType = "text/plain";
            await this._client.PutObjectAsync(new()
            {
                Key = key,
                BucketName = bucketName,
                ContentBody = content,
                ContentType = contentType,
            }, CurrentCT);
            var file = await downloader.DownloadAsync($"s3://{bucketName}/{key}", CurrentCT);
            Assert.Equal(file.FileName, key);
            Assert.Equal(file.FileType, contentType);
            Assert.Equal(file.FileSize, content.Length);
            using var reader = new StreamReader(await file.GetStreamAsync());
            var readContent = await reader.ReadToEndAsync(CurrentCT);
            Assert.Equal(content, readContent);
        }

        [Fact]
        public async Task DownloadAsync_NotExist_Success()
        {
            await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            {
                await downloader.DownloadAsync($"s3://nonexist-bucket/{Guid.NewGuid()}.txt", CurrentCT);
            });
        }

        /// <summary>
        /// Create a random bucket.
        /// </summary>
        /// <returns>Created bucket name.</returns>
        private async Task<string> CreateBucketAsync(CancellationToken token = default)
        {
            var bucketName = Guid.NewGuid().ToString();
            await this._client.PutBucketAsync(bucketName, token);
            return bucketName;
        }

    }
}
