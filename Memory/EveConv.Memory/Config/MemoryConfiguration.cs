using Microsoft.Extensions.Options;

namespace EveConv.Memory.Config;

/// <summary>
/// Configuration for the EveConv memory system.
/// Bound from the <c>"Memory"</c> section of <c>appsettings.json</c>.
/// </summary>
public class MemoryConfiguration : IOptions<MemoryConfiguration>
{
    /// <summary>
    /// Number of most recent messages to keep in recent memory cache.
    /// </summary>
    public int RecentMemoryCount { get; init; } = 20;

    /// <summary>
    /// Time-to-live for the recent memory cache entries.
    /// </summary>
    public TimeSpan RecentMemoryCacheTtl { get; init; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Default context window size in tokens. When the model-facing history exceeds this,
    /// compaction is triggered.
    /// </summary>
    public int DefaultContextWindowTokens { get; init; } = 4096;

    /// <summary>
    /// Number of most recent raw messages to reserve (not compact) for fidelity.
    /// </summary>
    public int CompactReserveRecentCount { get; init; } = 5;

    /// <summary>
    /// Maximum number of compaction levels allowed.
    /// </summary>
    public int MaxCompactionLevel { get; init; } = 3;

    /// <summary>
    /// Maximum number of sessions to batch for long-memory extraction.
    /// </summary>
    public int ExtractionSessionBatchSize { get; init; } = 10;

    /// <summary>
    /// Maximum total tokens for long-memory context messages injected into each request.
    /// </summary>
    public int LongMemoryMaxTokens { get; init; } = 500;

    /// <summary>
    /// Minimum importance score (0.0 to 1.0) for extracted long-memory entries to be retained.
    /// </summary>
    public float ImportanceThreshold { get; init; } = 0.5f;

    public MemoryConfiguration Value => this;
}