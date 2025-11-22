// https://github.com/microsoft/kernel-memory/blob/bd8d34e67dcd2b52acb408661d58b648453efbd3/service/Abstractions/Models/StreamableFileContent.cs
// Copyright (c) Microsoft. All rights reserved.

using System;
using System.IO;
using System.Threading.Tasks;

namespace EveConv.Abstraction.FileStorage;

public sealed class StreamableFileContent : IDisposable
{
    private Stream? _stream;

    public string FileName { get; } = string.Empty;
    public long FileSize { get; } = 0;
    public string FileType { get; } = string.Empty;
    public DateTimeOffset LastWrite { get; } = default;
    public Func<Task<Stream>> GetStreamAsync { get; }

    public StreamableFileContent()
    {
        this.GetStreamAsync = () => Task.FromResult<Stream>(new MemoryStream());
    }

    public StreamableFileContent(
        string fileName,
        long fileSize,
        string fileType = "application/octet-stream",
        DateTimeOffset lastWriteTimeUtc = default,
        Func<Task<Stream>>? asyncStreamDelegate = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(asyncStreamDelegate);

        this.FileName = fileName;
        this.FileSize = fileSize;
        this.FileType = fileType;
        this.LastWrite = lastWriteTimeUtc;
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