namespace EveConv.Abstraction.Memory;

/// <summary>
/// A single long-term memory entry extracted by LLM from session history.
/// Represents a user preference, habit, event, or fact that persists across sessions.
/// </summary>
public sealed class LongMemoryEntry
{
    /// <summary>
    /// Unique identifier for this entry.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Opaque partition key. Defaults to <c>"default"</c> for single-user scenarios.
    /// </summary>
    public required string OwnerKey { get; init; }

    /// <summary>
    /// The category of this memory: <c>preference</c>, <c>habit</c>, <c>event</c>, or <c>fact</c>.
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// The content of this memory entry.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// The session IDs from which this entry was extracted.
    /// </summary>
    public IReadOnlyList<string> SourceSessionIds { get; init; } = [];

    /// <summary>
    /// Importance score (0.0 to 1.0). Entries below <see cref="MemoryConfiguration.ImportanceThreshold"/> may be pruned.
    /// </summary>
    public float Importance { get; set; }

    /// <summary>
    /// When this entry was first created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// When this entry was last reinforced (re-extracted or confirmed).
    /// </summary>
    public DateTimeOffset LastReinforcedAt { get; set; }
}
