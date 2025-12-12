// https://github.com/microsoft/kernel-memory/blob/bd8d34e67dcd2b52acb408661d58b648453efbd3/service/Abstractions/Pipeline/MimeTypes.cs
// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace EveConv.Abstraction
{
    public interface IMimeTypeDetection
    {
        /// <summary>
        /// The default mime type.
        /// </summary>
        public const string OCTET_STREAM_MIME_TYPE = "application/octet-stream";

        /// <summary>
        /// Determines the file mime type based on file name or extension.
        /// </summary>
        /// <param name="filename">File name contains extension.</param>
        /// <returns>Mime type if determined.</returns>
        /// <remarks>DO NOT Mime-Sniffing(reading file content), may cause XSS or other security issues.</remarks>
        public string GetFileType(string filename);

        /// <summary>
        /// Try to determine the file mime type based on file name or extension.
        /// </summary>
        /// <param name="filename">File name contains extension.</param>
        /// <param name="mimeType">Mime type if determined, otherwise <see cref="OCTET_STREAM_MIME_TYPE"/>.</param>
        /// <returns><see langword="true"/> if determined successfully, otherwise <see langword="false"/>.</returns>
        public bool TryGetFileType(string filename, [NotNull] out string? mimeType)
        {
            try
            {
                mimeType = GetFileType(filename);
                return true;
            }
            catch
            {
            }
            mimeType = OCTET_STREAM_MIME_TYPE;
            return false;
        }
    }
}
