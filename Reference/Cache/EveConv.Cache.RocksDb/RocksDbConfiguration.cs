using Microsoft.Extensions.Options;

namespace EveConv.Cache.RocksDb;

/// <summary>
/// Configuration options for the RocksDB cache implementation.
/// </summary>
public class RocksDbConfiguration : IOptions<RocksDbConfiguration>
{
    /// <summary>
    /// Gets or sets the path to the RocksDB database directory.
    /// Defaults to "rocksdb-cache" in the current directory.
    /// </summary>
    public string DatabasePath { get; set; } = "rocksdb-cache";

    /// <summary>
    /// Gets or sets a value indicating whether to create the database if it does not exist.
    /// </summary>
    public bool CreateIfMissing { get; set; } = true;

    /// <summary>
    /// Gets or sets the default time-to-live for cached items when not specified.
    /// If null, items will not expire by default.
    /// </summary>
    public TimeSpan? DefaultTtl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to keep log files.
    /// </summary>
    public bool KeepLogFileNum { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of log files to keep.
    /// </summary>
    public ulong MaxLogFileSize { get; set; } = 0;

    /// <summary>
    /// Gets or sets the maximum number of open files. -1 means no limit.
    /// </summary>
    public int MaxOpenFiles { get; set; } = -1;

    /// <summary>
    /// Gets or sets the write buffer size in bytes.
    /// </summary>
    public ulong WriteBufferSize { get; set; } = 64 * 1024 * 1024; // 64MB

    /// <summary>
    /// Gets or sets the maximum write buffer number.
    /// </summary>
    public int MaxWriteBufferNumber { get; set; } = 3;

    /// <summary>
    /// Gets or sets the target file size base in bytes.
    /// </summary>
    public ulong TargetFileSizeBase { get; set; } = 64 * 1024 * 1024; // 64MB

    /// <summary>
    /// Gets the current configuration instance.
    /// </summary>
    public RocksDbConfiguration Value => this;
}
