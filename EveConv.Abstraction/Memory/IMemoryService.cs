using Microsoft.Extensions.AI;

namespace EveConv.Abstraction.Memory;

/// <summary>
/// Unified entry point for the EveConv memory system.
/// Provides request-time memory context for LLM execution and message persistence.
/// </summary>
public interface IMemoryService
{
    /// <summary>
    /// Returns only the request-time memory context needed for LLM execution.
    /// Does NOT return the full persisted session history; higher layers page/query that separately.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The <see cref="ChatContext"/> containing model-facing messages and long-term memory.</returns>
    Task<ChatContext> GetContextAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Persists a chat message to the memory system.
    /// This is the canonical write path for adding messages.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="message">The <see cref="ChatMessage"/> to persist.</param>
    /// <param name="ct">A cancellation token.</param>
    Task AddMessageAsync(string sessionId, ChatMessage message, CancellationToken ct = default);

    /// <summary>
    /// Triggers session compaction for the given session.
    /// Older messages are compacted via LLM summarization when the context window is exceeded.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ct">A cancellation token.</param>
    Task CompactSessionAsync(string sessionId, CancellationToken ct = default);
}
