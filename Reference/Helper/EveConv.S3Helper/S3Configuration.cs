using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.S3Helper
{
    public class S3Configuration
    {
        #region credential
        /// <summary>
        /// Used to create a basic credential using Access Key and Secret Key.
        /// </summary>
        public string? AccessKey { get => string.IsNullOrWhiteSpace(field) ? null : field; init; }
        /// <summary>
        /// Used to create a basic credential using Access Key and Secret Key.
        /// </summary>
        public string? SecretKey { get => string.IsNullOrWhiteSpace(field) ? null : field; init; }
        /// <summary>
        /// Used to create a basic credential using Access Key and Secret Key with additional Account ID.
        /// </summary>
        public string? AccountId { get => string.IsNullOrWhiteSpace(field) ? null : field; init; }
        /// <summary>
        /// File path to credentials profile location.
        /// </summary>
        /// <remarks>
        /// Used to create a profile based credential.
        /// </remarks>
        public string? ProfileLocation { get => string.IsNullOrWhiteSpace(field) ? null : field; init; }
        /// <summary>
        /// Profile name in <see cref="ProfileLocation"/>.
        /// <remarks>
        /// Used to create a profile based credential.
        /// </remarks>
        /// </summary>
        public string? ProfileName { get => string.IsNullOrWhiteSpace(field) ? null : field; init; }
        #endregion

        #region config
        /// <summary>
        /// Custom endpoint URL.
        /// </summary>
        /// <remarks>
        /// Got from environment or profile is not set.
        /// </remarks>
        public string? Endpoint { get => string.IsNullOrWhiteSpace(field) ? null : field; init; }
        #endregion

        #region operation
        /// <summary>
        /// Part size in bytes for upload.
        /// </summary>
        public long UploadPartSize { get; init; } = 5 * 1024 * 1024; // 5 MB
        #endregion
    }
}
