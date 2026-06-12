using Microsoft.Extensions.AI;

namespace EveConv.Abstraction.Memory;

/// <summary>
/// Persistence contract for session data.
/// Abstracts the underlying storage (RDB or in-memory) behind a common interface.
/// </summary>
public interface ISessionMemory
{
    /// <summary>
    /// Retrieves all messages for a session, ordered by sequence number.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A read-only list of <see cref="ChatMessage"/> instances.</returns>
    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a single message by its unique identifier.
    /// </summary>
    /// <param name="messageId">The message identifier.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The <see cref="ChatMessage"/> if found, or null.</returns>
    Task<ChatMessage?> GetMessageByIdAsync(string messageId, CancellationToken ct = default);

    /// <summary>
    /// Persists messages for a session using upsert semantics.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="messages">The messages to save.</param>
    /// <param name="ct">A cancellation token.</param>
    Task SaveMessagesAsync(string sessionId, IEnumerable<ChatMessage> messages, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all compaction records for a session, ordered by compaction level.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A read-only list of <see cref="SessionCompaction"/> instances.</returns>
    Task<IReadOnlyList<SessionCompaction>> GetCompactionsAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Persists a new compaction record.
    /// </summary>
    /// <param name="compaction">The compaction to save.</param>
    /// <param name="ct">A cancellation token.</param>
    Task SaveCompactionAsync(SessionCompaction compaction, CancellationToken ct = default);

    /// <summary>
    /// Retrieves session metadata.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The <see cref="ChatSession"/> if found, or null.</returns>
    Task<ChatSession?> GetSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Persists or updates session metadata.
    /// </summary>
    /// <param name="session">The session metadata to save.</param>
    /// <param name="ct">A cancellation token.</param>
    Task SaveSessionAsync(ChatSession session, CancellationToken ct = default);
}
