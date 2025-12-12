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

        /// <summary>
        /// Asynchronously create a new S3 bucket.
        /// </summary>
        /// <param name="bucketName">Name of bucket.</param>
        /// <param name="cancellationToken">Task cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
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

        /// <summary>
        /// Asynchronously ensure a S3 bucket exists, create it if not exists.
        /// </summary>
        /// <param name="bucketName">Name of bucket.</param>
        /// <param name="cancellationToken">Task cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
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

        /// <summary>
        /// Asynchronously delete objects in specified bucket which match <paramref name="prefix"/>.
        /// </summary>
        /// <param name="bucketName">Name of bucket.</param>
        /// <param name="prefix">Prefix which need to delete. Doesn't support wild chars.</param>
        /// <param name="cancellationToken">Task cancellation token.</param>
        /// <returns>
        ///     A task contains delete infos.
        ///     The first item is successed keys, and the second item is failed keys.
        ///     Delete may failed if access denied, object locked, etc.
        /// </returns>
        public async Task<(List<string> Successed, List<string> Failed)> DeleteObjectsAsync(string bucketName, string prefix, CancellationToken cancellationToken = default)
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
                if (objects.S3Objects is null || objects.S3Objects.Count == 0)
                {
                    break;
                }
                var deletes = await _client.DeleteObjectsAsync(new()
                {
                    BucketName = bucketName,
                    Quiet = false,
                    Objects = objects.S3Objects.ConvertAll(o => new KeyVersion { Key = o.Key }),
                }, cancellationToken);
                successed.AddRange((deletes.DeletedObjects ?? []).ConvertAll(o => o.Key));
                failed.AddRange((deletes.DeleteErrors ?? []).ConvertAll(e => e.Key));
                if (deletes.DeleteErrors?.Count > 0)
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

        /// <summary>
        /// Asynchronously retrieves metadata of object.
        /// </summary>
        /// <param name="bucketName">Name of bucket.</param>
        /// <param name="key">Key of object need to retrieve.</param>
        /// <param name="cancellationToken">Task cancellation token.</param>
        /// <returns>A task contains metadata.</returns>
        /// <remarks>Sames as <see cref="GetObjectAsync(string, string, CancellationToken)"/> but not contains content.</remarks>
        /// <seealso cref="GetObjectAsync(string, string, CancellationToken)"/>
        public async Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return await _client.GetObjectMetadataAsync(new()
            {
                BucketName = bucketName,
                Key = key,
            }, cancellationToken);
        }

        /// <summary>
        /// Asynchronously retrieves object from S3.
        /// </summary>
        /// <param name="bucketName">Name of bucket.</param>
        /// <param name="key">Key of object need to retrieve.</param>
        /// <param name="cancellationToken">Task cancellation token.</param>
        /// <returns>A task contains metadata and content.</returns>
        /// <see cref="GetObjectMetadataAsync(string, string, CancellationToken)"/>
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

        /// <summary>
        /// Asynchronously uploads content stream to S3 as an object.
        /// </summary>
        /// <param name="bucketName">Name of bucket.</param>
        /// <param name="key">Key of object that upload to.</param>
        /// <param name="fileContent">A content stream.</param>
        /// <param name="contentType">Content-Type, known as Mime-Type</param>
        /// <param name="cancellationToken">Task cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task UploadObjectAsync(string bucketName, string key, Stream fileContent, string? contentType = null, CancellationToken cancellationToken = default)
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
                ContentType = contentType,
            }, cancellationToken);
        }

        #endregion

    }
}
