using System.Text;
using Amazon.S3;
using EveConv.Abstraction;
using Renci.SshNet.Security;
using Testcontainers.Minio;

namespace EveConv.FileStorage.S3.Tests
{
    public class S3FileStorageTests : IClassFixture<MinioContainerFixture>, IAsyncLifetime
    {
        private readonly MinioContainer container;
        private readonly S3FileStorage storage;

        private AmazonS3Client _client = null!;

        private static CancellationToken CurrentCT => TestContext.Current.CancellationToken;

        public S3FileStorageTests(MinioContainerFixture fixture)
        {
            this.container = fixture.Container;
            var config = new S3Configuration()
            {
                AccessKey = container.GetAccessKey(),
                SecretKey = container.GetSecretKey(),
                Endpoint = container.GetConnectionString(),
            };
            this.storage = new(config, new MockMimeTypeDetection());
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
        public async Task ReadFileAsync_Exist_Success()
        {
            var indexName = await CreateBucketAsync(CurrentCT);
            var fileId = Guid.NewGuid().ToString();
            var fileName = $"{Guid.NewGuid()}.txt";
            var content = Guid.NewGuid().ToString();
            await this._client.PutObjectAsync(new()
            {
                BucketName = indexName,
                Key = $"{fileId}/{fileName}",
                ContentBody = content,
            }, CurrentCT);
            var file = await this.storage.ReadFileAsync(indexName, fileId, fileName, CurrentCT);
            Assert.Equal(fileName, file.FileName);
            Assert.Equal("text/plain", file.FileType);
            Assert.Equal(36, file.FileSize); // length of Guid string
            using var stream = await file.GetStreamAsync();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var result = await reader.ReadToEndAsync(CurrentCT);
            Assert.Equal(content, result);
        }

        [Fact]
        public async Task ReadFileAsync_FileNotExist_FileNotFoundException()
        {
            var b = await CreateBucketAsync(CurrentCT);
            var k = Guid.NewGuid().ToString();
            await Assert.ThrowsAsync<FileNotFoundException>(async () => await this.storage.ReadFileAsync(b, k, k, CurrentCT));
        }

        [Fact]
        public async Task ReadFileAsync_BucketNotExist_FileNotFoundException()
        {
            var k = Guid.NewGuid().ToString();
            await Assert.ThrowsAsync<FileNotFoundException>(async () => await this.storage.ReadFileAsync(k, k, k, CurrentCT));
        }

        private class MockMimeTypeDetection : IMimeTypeDetection
        {
            public string GetFileType(string filename)
            {
                return "text/plain";
            }
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
