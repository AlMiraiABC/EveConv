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
using Microsoft.Extensions.Logging;

namespace EveConv.S3Helper
{
    public class S3ObjectHelper
    {
        private readonly ILogger<S3ObjectHelper> _logger;

        private readonly S3Configuration _config;
        private readonly AmazonS3Client _client;
        private readonly TransferUtility _transferUtility;

        public S3ObjectHelper(S3Configuration config, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(config);
            this._config = config;
            this._logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<S3ObjectHelper>();
            this._client = CreateClient();
            this._transferUtility = new(this._client);
        }

        #region client

        private AmazonS3Client CreateClient()
        {
            var credentials = CreateCredentials();
            var config = new AmazonS3Config()
            {
                ServiceURL = _config.Endpoint,
            };
            return new AmazonS3Client(credentials, config);
        }

        private AWSCredentials CreateCredentials()
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

        #endregion

        #region bucket
        public async Task CreateBucketAsync(string bucketName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
            await this._client.PutBucketAsync(new PutBucketRequest()
            {
                BucketName = bucketName,
                UseClientRegion = true,
            }, cancellationToken);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Created bucket {idx}", bucketName);
            }
        }

        public async Task EnsureBucketExistsAsync(string bucketName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
            var exists = await AmazonS3Util.DoesS3BucketExistV2Async(this._client, bucketName);
            if (exists)
            {
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("Bucket {bucket} already exists", bucketName);
                }
            }
            try
            {
                await CreateBucketAsync(bucketName, cancellationToken);
            }
            catch (BucketAlreadyOwnedByYouException)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Bucket {bucket} already exists and owned by you", bucketName);
                }
            }
        }

        #endregion

        #region object

        public async Task<(List<string>, List<string>)> DeleteObjectsAsync(string bucketName, string prefix, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
            ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
            List<string> successed = [], failed = [];
            string? continuationToken = null;
            do
            {
                var objects = await _client.ListObjectsV2Async(new()
                {
                    BucketName = bucketName,
                    Prefix = prefix,
                    ContinuationToken = continuationToken,
                }, cancellationToken);
                continuationToken = (objects.IsTruncated ?? true) ? objects.NextContinuationToken : null;
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("Got {count} objects for deletion in bucket '{bucket}' with prefix '{prefix}'", objects.S3Objects.Count, bucketName, prefix);
                }
                if (objects.S3Objects.Count == 0)
                {
                    break;
                }
                var deletes = await _client.DeleteObjectsAsync(new()
                {
                    BucketName = bucketName,
                    Quiet = false,
                    Objects = objects.S3Objects.ConvertAll(o => new KeyVersion { Key = o.Key }),
                }, cancellationToken);
                successed.AddRange(deletes.DeletedObjects.ConvertAll(o => o.Key));
                failed.AddRange(deletes.DeleteErrors.ConvertAll(e => e.Key));
                if (deletes.DeletedObjects.Count > 0)
                {
                    if (_logger.IsEnabled(LogLevel.Warning))
                    {
                        _logger.LogWarning("Failed to delete {count} objects in bucket '{bucket}' with prefix '{prefix}'",
                            deletes.DeleteErrors.Count, bucketName, prefix);
                    }
                    if (_logger.IsEnabled(LogLevel.Trace))
                    {
                        foreach (var err in deletes.DeleteErrors)
                        {
                            _logger.LogTrace("Delete error: {key} - {code} - {msg}", err.Key, err.Code, err.Message);
                        }
                    }
                }
            } while (continuationToken is not null);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Deleted {del}/{all} objects in bucket '{bucket}' with prefix '{prefix}'",
                    successed.Count, successed.Count + failed.Count, bucketName, prefix);
            }
            return (successed, failed);
        }

        public async Task<GetObjectResponse> GetObjectAsync(string bucketName, string key, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            // HACK: How about return a temporary read-only file URL, that download by caller or user.
            return await _client.GetObjectAsync(new()
            {
                BucketName = bucketName,
                Key = key,
            }, cancellationToken);
        }

        public async Task UploadObjectAsync(string bucketName, string key, Stream fileContent, CancellationToken cancellationToken = default)
        {
            // multi part upload need split stream to chunk, that read all content from stream.
            ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(fileContent);
            await _transferUtility.UploadAsync(new()
            {
                BucketName = bucketName,
                Key = key,
                InputStream = fileContent,
                PartSize = _config.UploadPartSize,

            }, cancellationToken);
        }

        #endregion

    }
}
