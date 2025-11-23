using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Options;

namespace EveConv.FileStorage.S3
{
    public class S3Configuration : IOptions<S3Configuration>
    {
        #region aws credential
        /// <summary>
        /// Used to create a basic AWS credential using Access Key and Secret Key.
        /// </summary>
        public string? AccessKey { get => string.IsNullOrWhiteSpace(field) ? null : field; init; }
        /// <summary>
        /// Used to create a basic AWS credential using Access Key and Secret Key.
        /// </summary>
        public string? SecretKey { get => string.IsNullOrWhiteSpace(field) ? null : field; init; }
        /// <summary>
        /// Used to create a basic AWS credential using Access Key and Secret Key with additional Account ID.
        /// </summary>
        public string? AccountId { get => string.IsNullOrWhiteSpace(field) ? null : field; init; }
        /// <summary>
        /// File path to AWS credentials profile location.
        /// <para/>
        /// Used to create a profile based AWS credential.
        /// </summary>
        public string? ProfileLocation { get => string.IsNullOrWhiteSpace(field) ? null : field; init; }
        /// <summary>
        /// Profile name in <see cref="ProfileLocation"/>.
        /// <para/>
        /// Used to create a profile based AWS credential.
        /// </summary>
        public string? ProfileName { get => string.IsNullOrWhiteSpace(field) ? null : field; init; }
        #endregion

        #region aws config
        public string? RegionName { get => string.IsNullOrWhiteSpace(field) ? null : field; init; }
        #endregion

        #region operation
        /// <summary>
        /// Part size in bytes for upload.
        /// </summary>
        public long UploadPartSize { get; init; } = 5 * 1024 * 1024; // 5 MB
        #endregion

        public S3Configuration Value => this;
    }
}
