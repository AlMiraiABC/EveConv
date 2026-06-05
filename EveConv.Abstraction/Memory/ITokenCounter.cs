using Microsoft.Extensions.AI;

namespace EveConv.Abstraction.Memory;

/// <summary>
/// Provides lightweight token counting for compaction decisions.
/// Uses a character-based heuristic (~4 chars/token) — no heavy tokenizer libraries required.
/// </summary>
public interface ITokenCounter
{
    /// <summary>
    /// Estimates the token count for a single text string.
    /// </summary>
    /// <param name="text">The text to count tokens for.</param>
    /// <returns>The estimated token count.</returns>
    int CountTokens(string text);

    /// <summary>
    /// Estimates the total token count for a list of chat messages.
    /// </summary>
    /// <param name="messages">The messages to count tokens for.</param>
    /// <returns>The estimated total token count.</returns>
    int CountTokens(IReadOnlyList<ChatMessage> messages);
}
