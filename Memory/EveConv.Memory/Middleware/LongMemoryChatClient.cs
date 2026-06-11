using System.Runtime.CompilerServices;
using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.Memory;
using EveConv.Memory.Models;
using EveConv.Memory.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EveConv.Memory.Middleware;

/// <summary>
/// <see cref="DelegatingChatClient"/> middleware that injects long-term memory
/// context messages before each request, and triggers background extraction after.
/// </summary>
public sealed class LongMemoryChatClient : DelegatingChatClient
{
    private readonly ILongMemory _longMemory;
    private readonly LongMemoryOptions _options;
    private readonly ILogger _logger;

    // Simple debounce: extract at most once per N requests
    private int _requestCount;
    private const int ExtractionInterval = 10;

    /// <summary>
    /// Initializes a new instance of <see cref="LongMemoryChatClient"/>.
    /// </summary>
    /// <param name="inner">The inner <see cref="IChatClient"/>.</param>
    /// <param name="longMemory">The long-term memory manager.</param>
    /// <param name="options">Long memory options carrying the owner key.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    public LongMemoryChatClient(
        IChatClient inner,
        ILongMemory longMemory,
        IOptions<LongMemoryOptions> options,
        ILoggerFactory? loggerFactory = null)
        : base(inner)
    {
        _longMemory = longMemory;
        _options = options.Value;
        _logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<LongMemoryChatClient>();
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken ct = default)
    {
        var messageList = messages?.ToList() ?? [];

        // BEFORE: Inject long-term memory context
        var augmentedMessages = await AugmentWithLongMemoryAsync(messageList, ct);

        // Call inner client
        var response = await base.GetResponseAsync(augmentedMessages, options, ct);

        // AFTER: Fire-and-forget extraction (debounced)
        _ = ExtractInBackgroundAsync(messageList);

        return response;
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var messageList = messages?.ToList() ?? [];

        // BEFORE: Inject long-term memory context
        var augmentedMessages = await AugmentWithLongMemoryAsync(messageList, ct);

        await foreach (var update in base.GetStreamingResponseAsync(augmentedMessages, options, ct))
        {
            yield return update;
        }

        // AFTER: Fire-and-forget extraction (debounced)
        _ = ExtractInBackgroundAsync(messageList);
    }

    private async Task<IReadOnlyList<ChatMessage>> AugmentWithLongMemoryAsync(
        IReadOnlyList<ChatMessage> messages, CancellationToken ct)
    {
        try
        {
            var ownerKey = _options.OwnerKey;
            var longMemoryMessages = await _longMemory.GetContextMessagesAsync(ownerKey, ct);

            if (longMemoryMessages is null || longMemoryMessages.Count == 0)
            {
                return messages;
            }

            // Prepend long memory system messages before the chat history
            var augmented = new List<ChatMessage>(longMemoryMessages.Count + messages.Count);
            augmented.AddRange(longMemoryMessages);
            augmented.AddRange(messages);

            _logger.LogTrace("Injected {Count} long memory messages for owner {OwnerKey}",
                longMemoryMessages.Count, ownerKey);

            return augmented.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inject long memory — proceeding without it");
            return messages;
        }
    }

    private async Task ExtractInBackgroundAsync(IReadOnlyList<ChatMessage> messages)
    {
        try
        {
            var count = Interlocked.Increment(ref _requestCount);
            if (count % ExtractionInterval != 0)
            {
                return;
            }

            // Extract sessionId from messages
            var sessionIds = messages
                .Select(m => m.Contents.OfType<MemoryMetadataContent>().FirstOrDefault())
                .Where(m => m is not null)
                .Select(m => m!.SessionId)
                .Distinct()
                .ToList();

            if (sessionIds.Count == 0)
            {
                return;
            }

            await _longMemory.ExtractAndStoreAsync(_options.OwnerKey, sessionIds);
            _logger.LogTrace("Background long memory extraction completed for {Count} sessions",
                sessionIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background long memory extraction failed");
        }
    }
}
