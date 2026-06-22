using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.Memory;
using EveConv.Memory.Config;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EveConv.Memory.Services;

/// <summary>
/// Orchestrates the three memory tiers — Recent, Session, and Long — to build
/// the request-time <see cref="ChatContext"/> and manage message persistence.
/// </summary>
public sealed class MemoryService : IMemoryService
{
    public const string DEFAULT_OWNER_KEY = "default";

    private readonly IRecentMemory _recentMemory;
    private readonly ISessionMemory _session;
    private readonly ILongMemory _longMemory;
    private readonly EveConvChatReducer _chatReducer;
    private readonly ITokenCounter _tokenCounter;
    private readonly MemoryConfiguration _config;
    private readonly ILogger _logger;

    public MemoryService(
        IRecentMemory recentMemory,
        ISessionMemory session,
        ILongMemory longMemory,
        EveConvChatReducer chatReducer,
        ITokenCounter tokenCounter,
        IOptions<MemoryConfiguration> config,
        ILoggerFactory? loggerFactory = null)
    {
        _recentMemory = recentMemory;
        _session = session;
        _longMemory = longMemory;
        _chatReducer = chatReducer;
        _tokenCounter = tokenCounter;
        _config = config.Value;
        _logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<MemoryService>();
    }

    public Func<string>? GetOwnerKey { get; set; }

    /// <inheritdoc />
    public async Task<ChatContext> GetContextAsync(string sessionId, CancellationToken ct = default)
    {
        // 1. Get the raw session messages
        var rawMessages = await _session.GetMessagesAsync(sessionId, ct);

        // 2. Apply compaction via the reducer to get model-facing history
        var reducedMessages = await _chatReducer.ReduceAsync(rawMessages, ct);
        var modelMessages = reducedMessages?.ToList() ?? rawMessages.ToList();

        // 3. Get long-term memory context
        var longMemoryMessages =
            await _longMemory.GetContextMessagesAsync(GetOwnerKey?.Invoke() ?? DEFAULT_OWNER_KEY, ct);

        // 4. Calculate total tokens
        var allMessages = new List<ChatMessage>(modelMessages.Count + longMemoryMessages.Count);
        allMessages.AddRange(modelMessages);
        allMessages.AddRange(longMemoryMessages);
        var totalTokens = _tokenCounter.CountTokens(allMessages);

        return new ChatContext
        {
            SessionId = sessionId,
            ModelMessages = modelMessages,
            LongMemoryMessages = longMemoryMessages,
            TotalTokens = totalTokens
        };
    }

    /// <inheritdoc />
    public async Task AddMessageAsync(string sessionId, ChatMessage message, CancellationToken ct = default)
    {
        await _recentMemory.PushMessageAsync(sessionId, message, ct);

        // Ensure session metadata exists
        var session = await _session.GetSessionAsync(sessionId, ct);
        if (session is null)
        {
            session = new ChatSession
            {
                SessionId = sessionId,
                CreatedAt = DateTimeOffset.UtcNow,
                LastActiveAt = DateTimeOffset.UtcNow,
                TotalMessageCount = 1
            };
        }
        else
        {
            session.LastActiveAt = DateTimeOffset.UtcNow;
            session.TotalMessageCount++;
        }

        await _session.SaveSessionAsync(session, ct);
    }

    /// <inheritdoc />
    public async Task CompactSessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Triggering compaction for session {SessionId}", sessionId);
        }

        var messages = await _session.GetMessagesAsync(sessionId, ct);
        if (messages.Count == 0)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("No messages to compact for session {SessionId}", sessionId);
            }
            return;
        }

        await _chatReducer.ReduceAsync(messages, ct);
    }
}
