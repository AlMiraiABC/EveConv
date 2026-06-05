using Microsoft.Extensions.AI;

namespace EveConv.Memory.Models;

/// <summary>
/// Custom <see cref="AIContent"/> subclass that carries EveConv memory metadata on each <see cref="ChatMessage"/>.
/// Every message entering the memory system MUST have <see cref="MessageId"/> and <see cref="SessionId"/> set.
/// Registered via <c>AIJsonUtilities.AddAIContentType()</c>.
/// </summary>
public sealed class MemoryMetadataContent : AIContent
{
    /// <summary>
    /// The unique message identifier.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// The session identifier this message belongs to.
    /// </summary>
    public required string SessionId { get; init; }
}
