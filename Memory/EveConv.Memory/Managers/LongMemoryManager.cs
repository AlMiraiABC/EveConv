using System.Text.Json;
using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.Memory;
using EveConv.Memory.Models;
using EveConv.Memory.Options;
using EveConv.Memory.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EveConv.Memory.Managers;

/// <summary>
/// Manages long-term memory entries — individual facts/habits/preferences/events
/// extracted across sessions via LLM and persisted independently.
/// </summary>
public sealed class LongMemoryManager : ILongMemory
{
    private readonly LLMLongMemoryExtractor _extractor;
    private readonly ISessionMemory _session;
    private readonly ITokenCounter _tokenCounter;
    private readonly MemoryOptions _config;
    private readonly ILogger _logger;

    // In-memory fallback storage (used when ISqlSugarClient is not available)
    private static readonly Dictionary<string, List<LongMemoryEntryEntity>> _inMemoryStore = new();
    private static readonly Lock _lock = new();

    public LongMemoryManager(
        LLMLongMemoryExtractor extractor,
        ISessionMemory session,
        ITokenCounter tokenCounter,
        IOptions<MemoryOptions> config,
        ILoggerFactory? loggerFactory = null)
    {
        _extractor = extractor;
        _session = session;
        _tokenCounter = tokenCounter;
        _config = config.Value;
        _logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<LongMemoryManager>();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatMessage>> GetContextMessagesAsync(
        string ownerKey, CancellationToken ct = default)
    {
        var entries = await GetEntriesAsync(ownerKey);

        if (entries.Count == 0)
        {
            return [];
        }

        // Group by category, select top entries by importance
        var grouped = entries
            .GroupBy(e => e.Category)
            .OrderBy(g => g.Key);

        var result = new List<ChatMessage>();
        int totalTokens = 0;

        foreach (var group in grouped)
        {
            var bestEntry = group.OrderByDescending(e => e.Importance).First();
            var content = $"({bestEntry.Category}) {bestEntry.Content}";
            var tokens = _tokenCounter.CountTokens(content);

            if (totalTokens + tokens > _config.LongMemoryMaxTokens)
            {
                break; // Don't exceed token budget
            }

            result.Add(new ChatMessage(ChatRole.System, content));
            totalTokens += tokens;
        }

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Built {Count} long memory context messages ({Tokens} tokens) for owner {OwnerKey}",
                result.Count, totalTokens, ownerKey);
        }

        return result.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task ExtractAndStoreAsync(
        string ownerKey, IEnumerable<string> sessionIds, CancellationToken ct = default)
    {
        var sessionIdList = sessionIds.ToList();
        if (sessionIdList.Count == 0)
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Extracting long memory for owner {OwnerKey} from {Count} sessions",
                ownerKey, sessionIdList.Count);
        }

        // Load session compactions for context
        var compactionTexts = new List<string>();
        foreach (var sessionId in sessionIdList)
        {
            var compactions = await _session.GetCompactionsAsync(sessionId, ct);
            foreach (var compaction in compactions)
            {
                compactionTexts.Add(compaction.CompactedSummary);
            }

            // If no compactions, load raw messages
            if (compactions.Count == 0)
            {
                var messages = await _session.GetMessagesAsync(sessionId, ct);
                var text = string.Join("\n", messages
                    .Select(m => m.Contents.OfType<TextContent>().FirstOrDefault()?.Text ?? ""));
                if (!string.IsNullOrEmpty(text))
                {
                    compactionTexts.Add(text);
                }
            }
        }

        if (compactionTexts.Count == 0)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("No content to extract from sessions");
            }
            return;
        }

        // Use LLM to extract entries
        var extractedEntries = await _extractor.ExtractAsync(compactionTexts, sessionIdList, ct);

        // Upsert each entry
        foreach (var entry in extractedEntries)
        {
            await UpsertInternalAsync(ownerKey, entry);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Extracted and stored {Count} long memory entries for owner {OwnerKey}",
                extractedEntries.Count, ownerKey);
        }
    }

    /// <inheritdoc />
    public Task UpsertAsync(string ownerKey, LongMemoryEntry entry, CancellationToken ct = default)
    {
        return UpsertInternalAsync(ownerKey, entry);
    }

    /// <inheritdoc />
    public Task ForgetAsync(string ownerKey, string entryId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_inMemoryStore.TryGetValue(ownerKey, out var entries))
            {
                entries.RemoveAll(e => e.Id == entryId);
            }
        }

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Forgot long memory entry {EntryId} for owner {OwnerKey}", entryId, ownerKey);
        }
        return Task.CompletedTask;
    }

    private async Task UpsertInternalAsync(string ownerKey, LongMemoryEntry entry)
    {
        lock (_lock)
        {
            if (!_inMemoryStore.TryGetValue(ownerKey, out var entries))
            {
                entries = [];
                _inMemoryStore[ownerKey] = entries;
            }

            // Dedup by content similarity (simple hash-based)
            var contentHash = entry.Content.GetHashCode(StringComparison.Ordinal);
            var existing = entries.FirstOrDefault(e =>
                e.Content.GetHashCode(StringComparison.Ordinal) == contentHash);

            if (existing is not null)
            {
                // Update existing entry
                existing.Importance = Math.Max(existing.Importance, entry.Importance);
                existing.LastReinforcedAt = DateTime.UtcNow;

                // Merge source session IDs
                var existingSessions = DeserializeSessionIds(existing.SourceSessionIdsJson);
                foreach (var sid in entry.SourceSessionIds)
                {
                    if (!existingSessions.Contains(sid))
                    {
                        existingSessions.Add(sid);
                    }
                }
                existing.SourceSessionIdsJson = SerializeSessionIds(existingSessions);

                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("Reinforced existing long memory entry {EntryId}", existing.Id);
                }
            }
            else
            {
                // Add new entry
                var entity = new LongMemoryEntryEntity
                {
                    Id = entry.Id,
                    OwnerKey = ownerKey,
                    Category = entry.Category,
                    Content = entry.Content,
                    SourceSessionIdsJson = SerializeSessionIds(entry.SourceSessionIds),
                    Importance = entry.Importance,
                    CreatedAt = entry.CreatedAt.UtcDateTime,
                    LastReinforcedAt = entry.LastReinforcedAt.UtcDateTime
                };
                entries.Add(entity);

                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("Added new long memory entry {EntryId}", entry.Id);
                }
            }
        }

        await Task.CompletedTask;
    }

    private Task<List<LongMemoryEntryEntity>> GetEntriesAsync(string ownerKey)
    {
        lock (_lock)
        {
            if (_inMemoryStore.TryGetValue(ownerKey, out var entries))
            {
                return Task.FromResult(entries.ToList());
            }
        }

        return Task.FromResult(new List<LongMemoryEntryEntity>());
    }

    private static List<string> DeserializeSessionIds(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string SerializeSessionIds(IEnumerable<string> sessionIds)
    {
        return JsonSerializer.Serialize(sessionIds.ToList());
    }
}
