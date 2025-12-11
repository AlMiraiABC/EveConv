using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Testcontainers.Minio;

namespace EveConv.S3Helper.Tests
{
    public class S3ObjectHelperTests : IClassFixture<MinioContainerFixture>, IAsyncLifetime
    {
        private readonly MinioContainer container;
        private readonly S3ObjectHelper client;

        private AmazonS3Client _client = null!;

        public S3ObjectHelperTests(MinioContainerFixture fixture)
        {
            this.container = fixture.Container;
            var config = new S3Configuration()
            {
                AccessKey = container.GetAccessKey(),
                SecretKey = container.GetSecretKey(),
                Endpoint = container.GetConnectionString(),
            };
            this.client = new(config);
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
            var response = await _client.ListBucketsAsync(TestContext.Current.CancellationToken);
            Console.WriteLine("Got {0} buckets", response.Buckets?.Count ?? 0);
        }

        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            return;
        }

        [Fact]
        public async Task CreateBucketAsync_NotExist_Success()
        {
            await client.CreateBucketAsync(Guid.NewGuid().ToString(), TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task CreateBucketAsync_Exist_BucketAlreadyOwnedByYouException()
        {
            var bucketName = await CreateBucketAsync(TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<BucketAlreadyOwnedByYouException>(async () => await client.CreateBucketAsync(bucketName, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task EnsureBucketExistsAsync_NotExist_Success()
        {
            await client.EnsureBucketExistsAsync(Guid.NewGuid().ToString(), TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task EnsureBucketExistsAsync_Exist_Success()
        {
            var bucketName = await CreateBucketAsync(TestContext.Current.CancellationToken);
            await client.EnsureBucketExistsAsync(bucketName, TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task DeleteObjectsAsync_Exist_Success()
        {
            var bucketName = await CreateBucketAsync(TestContext.Current.CancellationToken);
            var prefix = Guid.NewGuid().ToString();
            await PutObjectsAsync(bucketName, prefix, Enumerable.Range(0, 10).Select(i => Guid.NewGuid().ToString()), TestContext.Current.CancellationToken);
            await this.client.DeleteObjectsAsync(bucketName, prefix, TestContext.Current.CancellationToken);
            var response = await this._client.ListObjectsV2Async(new()
            {
                BucketName = bucketName,
                Prefix = prefix,
            }, TestContext.Current.CancellationToken);
            Assert.Equal(0, response.KeyCount ?? 0);
        }

        [Fact]
        public async Task DeleteObjectsAsync_NotExist_Success()
        {
            var bucketName = await CreateBucketAsync(TestContext.Current.CancellationToken);
            var prefix = Guid.NewGuid().ToString();
            await this.client.DeleteObjectsAsync(bucketName, prefix, TestContext.Current.CancellationToken);
            var response = await this._client.ListObjectsV2Async(new()
            {
                BucketName = bucketName,
                Prefix = prefix,
            }, TestContext.Current.CancellationToken);
            Assert.Equal(0, response.KeyCount ?? 0);
        }

        [Fact]
        public async Task UploadObjectAsync_NotExist_Success()
        {
            var bucketName = await CreateBucketAsync(TestContext.Current.CancellationToken);
            var key = $"{Guid.NewGuid()}/{Guid.NewGuid()}.txt";
            var writeContent = Guid.NewGuid().ToString();
            using var fileContent = new MemoryStream(Encoding.UTF8.GetBytes(writeContent));
            await this.client.UploadObjectAsync(bucketName, key, fileContent, TestContext.Current.CancellationToken);
            var response = await this._client.GetObjectAsync(bucketName, key, TestContext.Current.CancellationToken);
            Assert.NotNull(response);
            using var reader = new StreamReader(response.ResponseStream);
            var readContent = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
            Assert.Equal(readContent, writeContent);
        }

        [Fact]
        public async Task UploadObjectAsync_Exist_Success()
        {
            var bucketName = await CreateBucketAsync(TestContext.Current.CancellationToken);
            var prefix = Guid.NewGuid().ToString();
            var writeContent = Guid.NewGuid().ToString();
            var fkey = (await PutObjectsAsync(bucketName, prefix, [writeContent], TestContext.Current.CancellationToken))[0];
            var key = $"{prefix}/{fkey}";
            using var fileContent = new MemoryStream(Encoding.UTF8.GetBytes(writeContent));
            await this.client.UploadObjectAsync(bucketName, key, fileContent, TestContext.Current.CancellationToken);
            var response = await this._client.GetObjectAsync(bucketName, key, TestContext.Current.CancellationToken);
            Assert.NotNull(response);
            using var reader = new StreamReader(response.ResponseStream);
            var readContent = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
            Assert.Equal(readContent, writeContent);
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

        /// <summary>
        /// Put objects to specified bucket with random key.
        /// </summary>
        /// <param name="bucketName">The specified bucket.</param>
        /// <param name="prefix">Prefix or folder that put to.</param>
        /// <param name="objects">Data to write to object.</param>
        /// <returns>Created object names(without prefix).</returns>
        private async Task<List<string>> PutObjectsAsync(string bucketName, string prefix, IEnumerable<string> objects, CancellationToken token = default)
        {
            if (prefix.EndsWith('/'))
            {
                prefix = prefix[..^1]; // remove suffix separator
            }
            var objs = objects.ToList();
            var keys = Enumerable.Range(1, objs.Count).Select(i => $"{i}.{Guid.NewGuid()}.txt").ToArray();
            var tasks = objs.Select(async (obj, idx) =>
            {
                var key = keys[idx];
                return await this._client.PutObjectAsync(new()
                {
                    BucketName = bucketName,
                    Key = $"{prefix}/{key}",
                    ContentBody = obj,
                }, TestContext.Current.CancellationToken);
            });
            await Task.WhenAll(tasks);
            return [.. keys];
        }
    }
}
