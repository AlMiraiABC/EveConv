using Microsoft.Extensions.AI;

namespace EveConv.Abstraction.Memory;

/// <summary>
/// Manages the most recent N chat messages per session,
/// persisted to both RDB and cache, and served as the full context for each LLM request.
/// </summary>
public interface IRecentMemory
{
    /// <summary>
    /// Retrieves the most recent messages for the given session.
    /// Checks cache first, falls back to DB on cache miss, and backfills cache.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="count">Maximum number of messages to retrieve.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A read-only list of the most recent <see cref="ChatMessage"/> instances.</returns>
    Task<IReadOnlyList<ChatMessage>> GetRecentAsync(string sessionId, int count, CancellationToken ct = default);

    /// <summary>
    /// Persists a message to both RDB and cache.
    /// Uses upsert semantics — pushing the same message twice is idempotent.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="message">The <see cref="ChatMessage"/> to persist.</param>
    /// <param name="ct">A cancellation token.</param>
    Task PushMessageAsync(string sessionId, ChatMessage message, CancellationToken ct = default);

    /// <summary>
    /// Trims the cached recent message list to the specified maximum count.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="maxCount">The maximum number of messages to retain in cache.</param>
    /// <param name="ct">A cancellation token.</param>
    Task TrimAsync(string sessionId, int maxCount, CancellationToken ct = default);
}
