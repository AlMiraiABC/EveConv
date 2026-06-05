namespace EveConv.Abstraction.Memory;

/// <summary>
/// Metadata for a chat session.
/// </summary>
public sealed class ChatSession
{
    /// <summary>
    /// The unique session identifier.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// When the session was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// When the session was last active.
    /// </summary>
    public DateTimeOffset LastActiveAt { get; set; }

    /// <summary>
    /// Total number of messages in this session.
    /// </summary>
    public int TotalMessageCount { get; set; }

    /// <summary>
    /// Aggregate total token count for all messages in this session (input + output).
    /// </summary>
    public int TotalTokenCount { get; set; }
}
