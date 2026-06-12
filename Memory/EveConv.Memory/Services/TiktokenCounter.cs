using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.Memory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace EveConv.Memory.Services;

/// <summary>
/// Lightweight token counter using a simple character-based heuristic (~4 chars/token).
/// Sufficient for compaction decisions — no heavy tokenizer libraries required.
/// </summary>
public sealed class TiktokenCounter : ITokenCounter
{
    private const double CharsPerToken = 4.0;

    private readonly ILogger _logger;

    public TiktokenCounter(ILoggerFactory? loggerFactory = null)
    {
        _logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<TiktokenCounter>();
    }

    /// <inheritdoc />
    public int CountTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return (int)Math.Ceiling(text.Length / CharsPerToken);
    }

    /// <inheritdoc />
    public int CountTokens(IReadOnlyList<ChatMessage> messages)
    {
        if (messages is null || messages.Count == 0)
        {
            return 0;
        }

        var total = 0;
        foreach (var message in messages)
        {
            // Count all text content from the message
            foreach (var content in message.Contents)
            {
                switch (content)
                {
                    case TextContent textContent:
                        total += CountTokens(textContent.Text);
                        break;
                    case DataContent dataContent:
                        // Estimate tokens for data content by URL length
                        total += CountTokens(dataContent.Uri ?? string.Empty);
                        break;
                }
            }

            // Add overhead for role + structure (~4 tokens per message)
            total += 4;
        }
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Counted {TokenCount} tokens for {MessageCount} messages", total, messages.Count);
        }
        return total;
    }
}
