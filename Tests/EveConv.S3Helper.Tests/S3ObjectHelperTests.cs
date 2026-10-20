using System.Text;
using Amazon.S3;
using Amazon.S3.Model;

namespace EveConv.S3Helper.Tests
{
    public class S3ObjectHelperTests : IAsyncLifetime
    {
        private readonly S3Configuration _s3config;
        private readonly S3ObjectHelper client;

        private AmazonS3Client _client = null!;

        private static CancellationToken CurrentCT => TestContext.Current.CancellationToken;

        public S3ObjectHelperTests()
        {
            dotenv.net.DotEnv.Load();
            var config = new S3Configuration()
            {
                AccessKey = Environment.GetEnvironmentVariable("S3_ACCESS_KEY") ?? throw new NullReferenceException("S3_ACCESS_KEY is required."),
                SecretKey = Environment.GetEnvironmentVariable("S3_SECRET_KEY") ?? throw new NullReferenceException("S3_SECRET_KEY is required."),
                Endpoint = Environment.GetEnvironmentVariable("S3_ENDPOINT") ?? throw new NullReferenceException("S3_ENDPOINT is required."),
                ForcePathStyle = Environment.GetEnvironmentVariable("S3_FORCE_PATH_STYLE") is not null && bool.Parse(Environment.GetEnvironmentVariable("S3_FORCE_PATH_STYLE")!),
            };
            this._s3config = config;
            this.client = new(config);
        }

        public async ValueTask InitializeAsync()
        {
            // test connection.
            this._client = new AmazonS3Client(
               _s3config.AccessKey,
               _s3config.SecretKey,
               new AmazonS3Config()
               {
                   ServiceURL = _s3config.Endpoint,
                   ForcePathStyle = _s3config.ForcePathStyle
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
        public async Task CreateBucketAsync_NotExist_Success()
        {
            await client.CreateBucketAsync(Guid.NewGuid().ToString(), CurrentCT);
        }

        [Fact]
        public async Task CreateBucketAsync_Exist_BucketAlreadyOwnedByYouException()
        {
            var bucketName = await CreateBucketAsync(CurrentCT);
            await Assert.ThrowsAsync<BucketAlreadyOwnedByYouException>(async () => await client.CreateBucketAsync(bucketName, CurrentCT));
        }

        [Fact]
        public async Task EnsureBucketExistsAsync_NotExist_Success()
        {
            await client.EnsureBucketExistsAsync(Guid.NewGuid().ToString(), CurrentCT);
        }

        [Fact]
        public async Task EnsureBucketExistsAsync_Exist_Success()
        {
            var bucketName = await CreateBucketAsync(CurrentCT);
            await client.EnsureBucketExistsAsync(bucketName, CurrentCT);
        }

        [Fact]
        public async Task DeleteObjectsAsync_Exist_Success()
        {
            var bucketName = await CreateBucketAsync(CurrentCT);
            var prefix = Guid.NewGuid().ToString();
            await PutObjectsAsync(bucketName, prefix, Enumerable.Range(0, 10).Select(i => Guid.NewGuid().ToString()), CurrentCT);
            await this.client.DeleteObjectsAsync(bucketName, prefix, CurrentCT);
            var response = await this._client.ListObjectsV2Async(new()
            {
                BucketName = bucketName,
                Prefix = prefix,
            }, CurrentCT);
            Assert.Equal(0, response.KeyCount ?? 0);
        }

        [Fact]
        public async Task DeleteObjectsAsync_NotExist_Success()
        {
            var bucketName = await CreateBucketAsync(CurrentCT);
            var prefix = Guid.NewGuid().ToString();
            await this.client.DeleteObjectsAsync(bucketName, prefix, CurrentCT);
            var response = await this._client.ListObjectsV2Async(new()
            {
                BucketName = bucketName,
                Prefix = prefix,
            }, CurrentCT);
            Assert.Equal(0, response.KeyCount ?? 0);
        }

        [Fact]
        public async Task UploadObjectAsync_NotExist_Success()
        {
            var bucketName = await CreateBucketAsync(CurrentCT);
            var key = $"{Guid.NewGuid()}/{Guid.NewGuid()}.txt";
            var writeContent = Guid.NewGuid().ToString();
            using var fileContent = new MemoryStream(Encoding.UTF8.GetBytes(writeContent));
            await this.client.UploadObjectAsync(bucketName, key, fileContent, null, CurrentCT);
            var response = await this._client.GetObjectAsync(bucketName, key, CurrentCT);
            Assert.NotNull(response);
            using var reader = new StreamReader(response.ResponseStream);
            var readContent = await reader.ReadToEndAsync(CurrentCT);
            Assert.Equal(readContent, writeContent);
        }

        [Fact]
        public async Task UploadObjectAsync_Exist_Success()
        {
            var bucketName = await CreateBucketAsync(CurrentCT);
            var prefix = Guid.NewGuid().ToString();
            var writeContent = Guid.NewGuid().ToString();
            var fkey = (await PutObjectsAsync(bucketName, prefix, [writeContent], CurrentCT))[0];
            var key = $"{prefix}/{fkey}";
            using var fileContent = new MemoryStream(Encoding.UTF8.GetBytes(writeContent));
            await this.client.UploadObjectAsync(bucketName, key, fileContent, null, CurrentCT);
            var response = await this._client.GetObjectAsync(bucketName, key, CurrentCT);
            Assert.NotNull(response);
            using var reader = new StreamReader(response.ResponseStream);
            var readContent = await reader.ReadToEndAsync(CurrentCT);
            Assert.Equal(readContent, writeContent);
        }

        [Fact]
        public async Task UploadObjectAsync_ContentTypeSpec_Success()
        {
            var bucketName = await CreateBucketAsync(CurrentCT);
            var contentType = "text/plain";
            var key = $"{Guid.NewGuid()}/{Guid.NewGuid()}.txt";
            var writeContent = Guid.NewGuid().ToString();
            using var fileContent = new MemoryStream(Encoding.UTF8.GetBytes(writeContent));
            await this.client.UploadObjectAsync(bucketName, key, fileContent, contentType, CurrentCT);
            var response = await this._client.GetObjectAsync(bucketName, key, CurrentCT);
            Assert.NotNull(response);
            Assert.Equal(contentType, response.Headers.ContentType);
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
                }, CurrentCT);
            });
            await Task.WhenAll(tasks);
            return [.. keys];
        }
    }
}
