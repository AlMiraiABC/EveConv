using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.Memory;
using EveConv.Memory.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace EveConv.Memory.Middleware;

/// <summary>
/// <see cref="DelegatingChatClient"/> middleware that automatically persists
/// all chat messages flowing through the pipeline via <see cref="IRecentMemory"/>.
/// This is the default automatic write path for the normal chat flow.
/// </summary>
public sealed class MemoryPersistingChatClient : DelegatingChatClient
{
    private readonly IRecentMemory _recentMemory;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="MemoryPersistingChatClient"/>.
    /// </summary>
    /// <param name="inner">The inner <see cref="IChatClient"/>.</param>
    /// <param name="recentMemory">The recent memory manager for persistence.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    public MemoryPersistingChatClient(
        IChatClient inner,
        IRecentMemory recentMemory,
        ILoggerFactory? loggerFactory = null)
        : base(inner)
    {
        _recentMemory = recentMemory;
        _logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<MemoryPersistingChatClient>();
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken ct = default)
    {
        var messageList = messages?.ToList() ?? [];

        // BEFORE: Persist incoming user/assistant messages
        await PersistIncomingMessagesAsync(messageList, ct);

        // Call inner client
        var response = await base.GetResponseAsync(messageList, options, ct);

        // AFTER: Persist response messages
        await PersistResponseMessagesAsync(messageList, response, ct);

        return response;
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var messageList = messages?.ToList() ?? [];

        // BEFORE: Persist incoming user/assistant messages
        await PersistIncomingMessagesAsync(messageList, ct);

        // Collect streamed updates for persistence
        var updates = new List<ChatResponseUpdate>();

        await foreach (var update in base.GetStreamingResponseAsync(messageList, options, ct))
        {
            updates.Add(update);
            yield return update;
        }

        // AFTER: Build a synthetic ChatResponse and persist
        if (updates.Count > 0)
        {
            var text = string.Concat(updates
                .Where(u => u.Text is not null)
                .Select(u => u.Text));

            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }
            var msg = new ChatMessage(ChatRole.Assistant, text);
            var sessionId = ExtractSessionId(messageList);
            if (!string.IsNullOrEmpty(sessionId))
            {
                await _recentMemory.PushMessageAsync(sessionId, msg, ct);
            }
        }
    }

    private async Task PersistIncomingMessagesAsync(
        IReadOnlyList<ChatMessage> messageList, CancellationToken ct)
    {
        foreach (var message in messageList)
        {
            var sessionId = ExtractSessionId(message);
            if (string.IsNullOrEmpty(sessionId))
            {
                // Try to get sessionId from earlier messages in the batch
                sessionId = ExtractSessionId(messageList);
            }

            if (string.IsNullOrEmpty(sessionId)) continue;

            // Ensure metadata is attached
            var metadata = message.Contents.OfType<MemoryMetadataContent>().FirstOrDefault();
            if (metadata is null)
            {
                metadata = new MemoryMetadataContent
                {
                    MessageId = Guid.NewGuid().ToString("N"),
                    SessionId = sessionId
                };
                message.Contents.Add(metadata);
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("Attached metadata to message {MessageId}", metadata.MessageId);
                }
            }

            await _recentMemory.PushMessageAsync(sessionId, message, ct);
        }
    }

    private async Task PersistResponseMessagesAsync(
        IReadOnlyList<ChatMessage> requestMessages,
        ChatResponse response,
        CancellationToken ct)
    {
        var sessionId = ExtractSessionId(requestMessages);
        if (string.IsNullOrEmpty(sessionId))
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Skipping response persistence: no sessionId in request messages");
            }
            return;
        }

        foreach (var msg in response.Messages)
        {
            await _recentMemory.PushMessageAsync(sessionId, msg, ct);
        }
    }

    private static string? ExtractSessionId(ChatMessage message)
    {
        return message.Contents
            .OfType<MemoryMetadataContent>()
            .FirstOrDefault()
            ?.SessionId;
    }

    private static string? ExtractSessionId(IReadOnlyList<ChatMessage> messages)
    {
        return messages.Select(ExtractSessionId).FirstOrDefault(sessionId => !string.IsNullOrEmpty(sessionId));
    }
}
