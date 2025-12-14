// https://github.com/microsoft/kernel-memory/blob/bd8d34e67dcd2b52acb408661d58b648453efbd3/service/Abstractions/Models/StreamableFileContent.cs
// Copyright (c) Microsoft. All rights reserved.

using System;
using System.IO;
using System.Threading.Tasks;

namespace EveConv.Abstraction;

public record StreamableFileContent : IDisposable
{
    private Stream? _stream;

    /// <summary>
    /// Name of file.
    /// </summary>
    public string FileName { get; } = string.Empty;
    /// <summary>
    /// Size of file in bytes, known as Content-Length.
    /// </summary>
    /// <remarks>Set to <c>-1</c> for unknown length.</remarks>
    public long FileSize { get; } = 0;
    /// <summary>
    /// File type, known as Content-Type or Mime-Type.
    /// </summary>
    public string FileType { get; } = string.Empty;
    /// <summary>
    /// Last write datetime in UTC. Default to <see cref="DateTimeOffset.UtcNow"/>.
    /// </summary>
    public DateTimeOffset LastWrite { get; } = DateTimeOffset.UtcNow;
    /// <summary>
    /// Callback to get content stream asynchronously.
    /// </summary>
    public Func<Task<Stream>> GetStreamAsync { get; }

    /// <summary>
    /// Inistializes a new empty instance of <see cref="StreamableFileContent"/>.
    /// </summary>
    public StreamableFileContent()
    {
        this.GetStreamAsync = () => Task.FromResult<Stream>(new MemoryStream());
    }

    /// <summary>
    /// Initializes a new instance of <see cref="StreamableFileContent"/> with specified props.
    /// </summary>
    /// <param name="fileName">File name. Cannot be null or whitespace.</param>
    /// <param name="fileSize">File size in bytes, known as Content-Length.</param>
    /// <param name="asyncStreamDelegate">Callback to get content stream asynchronously. Cannot be null.</param>
    /// <param name="fileType">File type, known as Content-Type or Mime-Type.</param>
    /// <param name="lastWriteTimeUtc">Last write datetime in UTC. Default to now.</param>
    public StreamableFileContent(
        string fileName,
        long fileSize,
        Func<Task<Stream>> asyncStreamDelegate,
        string? fileType = "application/octet-stream",
        DateTimeOffset? lastWriteTimeUtc = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(asyncStreamDelegate);

        this.FileName = fileName;
        this.FileSize = fileSize;
        this.FileType = fileType ?? "application/octet-stream";
        this.LastWrite = lastWriteTimeUtc.HasValue ? lastWriteTimeUtc.Value : DateTimeOffset.UtcNow;
        this.GetStreamAsync = async () =>
        {
            this._stream = await asyncStreamDelegate().ConfigureAwait(false);
            return this._stream;
        };
    }

    public void Dispose()
    {
        if (this._stream == null) { return; }

        this._stream.Close();
        this._stream.Dispose();
    }
}