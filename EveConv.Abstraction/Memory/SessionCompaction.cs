namespace EveConv.Abstraction.Memory;

/// <summary>
/// Represents a compaction of older session messages into an LLM-generated summary.
/// Enables traceability back to the original raw messages via <see cref="SourceMessageIds"/>.
/// </summary>
public sealed class SessionCompaction
{
    /// <summary>
    /// Unique identifier for this compaction record.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The session this compaction belongs to.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// The LLM-generated summary of the compacted messages.
    /// </summary>
    public required string CompactedSummary { get; init; }

    /// <summary>
    /// The message IDs of the original raw messages that were compacted into this summary.
    /// </summary>
    public IReadOnlyList<string> SourceMessageIds { get; init; } = [];

    /// <summary>
    /// Estimated token count of the original raw messages before compaction.
    /// </summary>
    public int OriginalTokenCount { get; init; }

    /// <summary>
    /// Estimated token count of the compacted summary.
    /// </summary>
    public int CompactedTokenCount { get; init; }

    /// <summary>
    /// The compaction level (1 = first compaction, 2 = re-compaction, etc.).
    /// </summary>
    public int CompactionLevel { get; init; }

    /// <summary>
    /// When this compaction was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }
}
