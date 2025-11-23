using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.Runtime.Credentials;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Amazon.S3.Util;
using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.FileStorage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EveConv.FileStorage.S3
{
    public class S3FileStorage : IFileStorage
    {
        private readonly S3Configuration _config;
        private readonly ILogger<S3FileStorage> _logger;

        private readonly AmazonS3Client _client;
        private readonly TransferUtility _transferUtility;

        public S3FileStorage(IOptions<S3Configuration> options, ILogger<S3FileStorage>? logger = null)
        {
            ArgumentNullException.ThrowIfNull(options);
            this._config = options.Value;
            this._logger = logger ?? DefaultLogger<S3FileStorage>.Instance;
            this._client = CreateAwsClient();
            this._transferUtility = new(this._client);
        }

        public AmazonS3Client CreateAwsClient()
        {
            var credentials = CreateAwsCredentials();
            var region = _config.RegionName is null ? null : RegionEndpoint.GetBySystemName(_config.RegionName);
            return new AmazonS3Client(credentials, region);
        }

        private AWSCredentials CreateAwsCredentials()
        {
            // 1. Configuration keys
            if (_config.AccessKey is not null && _config.SecretKey is not null)
            {
                var creds = new BasicAWSCredentials(_config.AccessKey, _config.SecretKey, _config.AccountId);
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Using AWS credentials from configuration");
                }
                return creds;
            }
            // 2. Environment variables
            try
            {
                var creds = new EnvironmentVariablesAWSCredentials();
                _ = creds.GetCredentials(); // force validation
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Using AWS credentials from environment variables");
                }
                return creds;
            }
            catch (InvalidOperationException)
            {
            }
            // 3. Shared profile
            try
            {
                var chain = new CredentialProfileStoreChain(_config.ProfileLocation);
                var profileName = _config.ProfileName ?? "default";
                if (chain.TryGetAWSCredentials(profileName, out var profileCreds))
                {
                    var creds = profileCreds;
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Using AWS credentials from profile '{profileName}'", profileName);
                    }
                    return creds;
                }
            }
            catch (Exception)
            {
            }
            return DefaultAWSCredentialsIdentityResolver.GetCredentials();
        }

        public async Task CreateIndexAsync(string indexName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
            try
            {
                var resp = await this._client.PutBucketAsync(new PutBucketRequest()
                {
                    BucketName = indexName,
                    UseClientRegion = true,
                }, cancellationToken);
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Created bucket {idx}", indexName);
                }
            }
            catch (BucketAlreadyOwnedByYouException)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Bucket {idx} already exists and owned by you", indexName);
                }
            }
        }

        public async Task DeleteFileAsync(string indexName, string fileId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileId);
            int allCount = 0, delCount = 0;
            string? continuationToken = null;
            do
            {
                var objects = await _client.ListObjectsV2Async(new()
                {
                    BucketName = indexName,
                    Prefix = $"{fileId}/",
                    ContinuationToken = continuationToken,
                }, cancellationToken);
                continuationToken = (objects.IsTruncated ?? true) ? objects.NextContinuationToken : null;
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("Got {count} objects for deletion in index '{idx}' with prefix '{fid}'", objects.S3Objects.Count, indexName, fileId);
                }
                allCount += objects.S3Objects.Count;
                if (objects.S3Objects.Count == 0)
                {
                    break;
                }
                var deletes = await _client.DeleteObjectsAsync(new()
                {
                    BucketName = indexName,
                    Quiet = true,
                    Objects = objects.S3Objects.ConvertAll(o => new KeyVersion { Key = o.Key }),
                }, cancellationToken);
                if (deletes.DeleteErrors.Count > 0)
                {
                    if (_logger.IsEnabled(LogLevel.Warning))
                    {
                        _logger.LogWarning("Failed to delete {count} objects in index '{idx}' with prefix '{fid}'",
                            deletes.DeleteErrors.Count, indexName, fileId);
                    }
                    if (_logger.IsEnabled(LogLevel.Trace))
                    {
                        foreach (var err in deletes.DeleteErrors)
                        {
                            _logger.LogTrace("Delete error: {key} - {code} - {msg}", err.Key, err.Code, err.Message);
                        }
                    }
                }
                delCount += objects.S3Objects.Count - deletes.DeleteErrors.Count;
            } while (continuationToken is not null);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Deleted {del}/{all} objects in index '{idx}' with prefix '{fid}'",
                    delCount, allCount, indexName, fileId);
            }
        }

        public async Task EnsureIndexExistsAsync(string indexName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
            var exists = await AmazonS3Util.DoesS3BucketExistV2Async(this._client, indexName);
            if (exists)
            {
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("Bucket {idx} already exists", indexName);
                }
            }
            await CreateIndexAsync(indexName, cancellationToken);
        }

        public async Task<StreamableFileContent> ReadFileAsync(string indexName, string fileId, string fileName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileId);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            var key = GetObjectKey(fileId, fileName);
            // HACK: How about return a temporary read-only file URL, that download by caller or user.
            try
            {
                var file = await _client.GetObjectAsync(new()
                {
                    BucketName = indexName,
                    Key = key,
                }, cancellationToken);
                return new StreamableFileContent(
                    fileName: fileName,
                    fileSize: file.ContentLength,
                    fileType: file.Headers.ContentType,
                    lastWriteTimeUtc: file.LastModified ?? default,
                    asyncStreamDelegate: async () => file.ResponseStream);
            }
            catch (NoSuchKeyException)
            {
                throw new FileNotFoundException($"File not found in bucket {indexName}", key);
            }
        }

        public async Task WriteFileAsync(string indexName, string fileId, string fileName, Stream fileContent, CancellationToken cancellationToken = default)
        {
            // multi part upload need split stream to chunk, that read all content from stream.
            ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileId);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            ArgumentNullException.ThrowIfNull(fileContent);
            await _transferUtility.UploadAsync(new()
            {
                BucketName = indexName,
                Key = GetObjectKey(fileId, fileName),
                InputStream = fileContent,
                PartSize = _config.UploadPartSize,

            }, cancellationToken);
        }

        #region private helper

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetObjectKey(string fileId, string fileName)
        {
            return $"{fileId}/{fileName}";
        }

        #endregion
    }
}
