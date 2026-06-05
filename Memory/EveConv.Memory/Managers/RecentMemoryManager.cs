using EveConv.Abstraction.Cache;
using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.Memory;
using EveConv.Memory.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EveConv.Memory.Managers;

/// <summary>
/// Manages the most recent N chat messages per session,
/// with cache-first reads and dual-write to cache + DB.
/// </summary>
public sealed class RecentMemoryManager : IRecentMemory
{
    private readonly MemoryConfiguration _config;
    private readonly ICache _cache;
    private readonly ISessionMemoryStore _store;
    private readonly ITokenCounter _tokenCounter;
    private readonly ILogger _logger;

    public RecentMemoryManager(
        IOptions<MemoryConfiguration> config,
        ICache cache,
        ISessionMemoryStore store,
        ITokenCounter tokenCounter,
        ILoggerFactory? loggerFactory = null)
    {
        _config = config.Value;
        _cache = cache;
        _store = store;
        _tokenCounter = tokenCounter;
        _logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<RecentMemoryManager>();
    }

    private static string CacheKey(string sessionId) => $"recent:{sessionId}";

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatMessage>> GetRecentAsync(
        string sessionId, int count, CancellationToken ct = default)
    {
        // 1. Try cache first
        var cached = await _cache.ListRangeAsync(CacheKey(sessionId), -count, -1, ct);
        if (cached is not null && cached.Count > 0)
        {
            _logger.LogTrace("Cache hit: {Count} messages for session {SessionId}", cached.Count, sessionId);
            return cached.Cast<ChatMessage>().ToList().AsReadOnly();
        }

        // 2. Cache miss — load from DB
        _logger.LogTrace("Cache miss for session {SessionId}, loading from store", sessionId);
        var messages = await _store.GetMessagesAsync(sessionId, ct);
        var recent = messages.TakeLast(count).ToList();

        // 3. Backfill cache
        if (recent.Count > 0)
        {
            foreach (var msg in recent)
            {
                await _cache.ListRightPushAsync(CacheKey(sessionId), msg, _config.RecentMemoryCacheTtl, ct);
            }
        }

        return recent.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task PushMessageAsync(
        string sessionId, ChatMessage message, CancellationToken ct = default)
    {
        // 1. Ensure metadata is attached
        var metadata = message.Contents.OfType<MemoryMetadataContent>().FirstOrDefault();
        if (metadata is null)
        {
            metadata = new MemoryMetadataContent
            {
                MessageId = Guid.NewGuid().ToString("N"),
                SessionId = sessionId
            };
            message.Contents.Add(metadata);
        }

        // 2. Persist to DB
        await _store.SaveMessagesAsync(sessionId, [message], ct);

        // 3. Push to cache list (right = most recent)
        var cacheKey = CacheKey(sessionId);
        var length = await _cache.ListRightPushAsync(cacheKey, message, _config.RecentMemoryCacheTtl, ct);

        // 4. Trim if exceeds max count
        if (length > _config.RecentMemoryCount)
        {
            var excess = length - _config.RecentMemoryCount;
            await _cache.ListLeftPopAsync(cacheKey, excess, ct);
        }

        _logger.LogTrace("Pushed message {MessageId} to session {SessionId}",
            metadata.MessageId, sessionId);
    }

    /// <inheritdoc />
    public async Task TrimAsync(string sessionId, int maxCount, CancellationToken ct = default)
    {
        var cacheKey = CacheKey(sessionId);
        var length = await _cache.ListLengthAsync(cacheKey, ct);

        if (length > maxCount)
        {
            var excess = length - maxCount;
            await _cache.ListLeftPopAsync(cacheKey, excess, ct);
            _logger.LogTrace("Trimmed {Excess} messages from session {SessionId}", excess, sessionId);
        }
    }
}
