using Microsoft.Extensions.Options;

namespace EveConv.Memory.Options;

/// <summary>
/// Configuration for the EveConv memory system.
/// Bound from the <c>"Memory"</c> section of appsettings.json.
/// </summary>
public class MemoryOptions : IOptions<MemoryOptions>
{
    /// <summary>
    /// Number of most recent messages to keep in recent memory cache.
    /// </summary>
    public int RecentMemoryCount { get; set; } = 20;

    /// <summary>
    /// Time-to-live for the recent memory cache entries.
    /// </summary>
    public TimeSpan RecentMemoryCacheTtl { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Default context window size in tokens. When the model-facing history exceeds this,
    /// compaction is triggered.
    /// </summary>
    public int DefaultContextWindowTokens { get; set; } = 4096;

    /// <summary>
    /// Number of most recent raw messages to reserve (not compact) for fidelity.
    /// </summary>
    public int CompactReserveRecentCount { get; set; } = 5;

    /// <summary>
    /// Model ID used for session compaction/summarization.
    /// Used by the startup project to register a keyed <c>IChatClient</c> with key <c>"summarization"</c>.
    /// </summary>
    public string CompactionModelId { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Maximum number of compaction levels allowed.
    /// </summary>
    public int MaxCompactionLevel { get; set; } = 3;

    /// <summary>
    /// Model ID used for long-term memory extraction.
    /// Used by the startup project to register a keyed <c>IChatClient</c> with key <c>"extraction"</c>.
    /// </summary>
    public string LongMemoryExtractionModelId { get; set; } = "gpt-4o";

    /// <summary>
    /// Maximum number of sessions to batch for long-memory extraction.
    /// </summary>
    public int ExtractionSessionBatchSize { get; set; } = 10;

    /// <summary>
    /// Maximum total tokens for long-memory context messages injected into each request.
    /// </summary>
    public int LongMemoryMaxTokens { get; set; } = 500;

    /// <summary>
    /// Minimum importance score (0.0 to 1.0) for extracted long-memory entries to be retained.
    /// </summary>
    public float ImportanceThreshold { get; set; } = 0.5f;

    public MemoryOptions Value => this;
}

/// <summary>
/// Options for long memory owner key resolution.
/// Single-user clients configure <c>"default"</c>; multi-user systems provide a custom
/// <see cref="EveConv.Abstraction.Memory.ILongMemoryOwnerKeyProvider"/>.
/// </summary>
public class LongMemoryOptions
{
    /// <summary>
    /// Opaque partition key for long-term memory. Defaults to <c>"default"</c> for single-user scenarios.
    /// </summary>
    public string OwnerKey { get; set; } = "default";
}