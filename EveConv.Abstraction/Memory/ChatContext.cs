using Microsoft.Extensions.AI;

namespace EveConv.Abstraction.Memory;

/// <summary>
/// The request-time memory context returned to the LLM pipeline.
/// Contains model-facing session history (after compaction) and long-term memory context.
/// </summary>
public sealed class ChatContext
{
    /// <summary>
    /// The session identifier this context belongs to.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Model-facing session history after compaction. This is the history sent to the LLM.
    /// It may contain compaction summaries instead of older raw messages.
    /// </summary>
    public IReadOnlyList<ChatMessage> ModelMessages { get; init; } = [];

    /// <summary>
    /// Long-term memory context messages, one per category (preference, habit, event, fact).
    /// Each is a <see cref="ChatRole.System"/> message with "(category) content" format.
    /// Empty if no long memories exist.
    /// </summary>
    public IReadOnlyList<ChatMessage> LongMemoryMessages { get; init; } = [];

    /// <summary>
    /// Total estimated token count for all messages in this context.
    /// </summary>
    public int TotalTokens { get; init; }
}
