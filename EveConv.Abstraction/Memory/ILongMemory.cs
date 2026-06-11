using Microsoft.Extensions.AI;

namespace EveConv.Abstraction.Memory;

/// <summary>
/// Manages cross-session long-term memory — user habits, preferences, events, and facts
/// extracted by LLM and injected into every request context.
/// </summary>
public interface ILongMemory
{
    /// <summary>
    /// Returns context messages for injection into the LLM request.
    /// One <see cref="ChatRole.System"/> message per category (preference, habit, event, fact),
    /// total tokens kept within the configured <c>LongMemoryMaxTokens</c> limit.
    /// Returns an empty list if no entries exist for the owner.
    /// </summary>
    /// <param name="ownerKey">Opaque partition key. Single-user clients pass <c>"default"</c>.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A read-only list of system <see cref="ChatMessage"/> instances.</returns>
    Task<IReadOnlyList<ChatMessage>> GetContextMessagesAsync(string ownerKey, CancellationToken ct = default);

    /// <summary>
    /// Triggers LLM-based extraction of long-term memory entries from recent sessions.
    /// Extracted entries are upserted individually (dedup by content similarity).
    /// </summary>
    /// <param name="ownerKey">Opaque partition key.</param>
    /// <param name="sessionIds">The session identifiers to analyze.</param>
    /// <param name="ct">A cancellation token.</param>
    Task ExtractAndStoreAsync(string ownerKey, IEnumerable<string> sessionIds, CancellationToken ct = default);

    /// <summary>
    /// Upserts a single long-term memory entry, deduplicating by content similarity.
    /// </summary>
    /// <param name="ownerKey">Opaque partition key.</param>
    /// <param name="entry">The entry to upsert.</param>
    /// <param name="ct">A cancellation token.</param>
    Task UpsertAsync(string ownerKey, LongMemoryEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Deletes a single long-term memory entry by its identifier.
    /// </summary>
    /// <param name="ownerKey">Opaque partition key.</param>
    /// <param name="entryId">The entry identifier to delete.</param>
    /// <param name="ct">A cancellation token.</param>
    Task ForgetAsync(string ownerKey, string entryId, CancellationToken ct = default);
}

/// <summary>
/// Provides the owner key for long-term memory partitioning.
/// Implement this interface for multi-user systems where the owner key is resolved per request.
/// </summary>
public interface ILongMemoryOwnerKeyProvider
{
    /// <summary>
    /// Resolves the owner key for the current context.
    /// </summary>
    /// <returns>The opaque partition key string.</returns>
    string GetOwnerKey();
}
