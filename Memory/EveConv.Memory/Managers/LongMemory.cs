using System.Text.Json;
using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.Memory;
using EveConv.Memory.Models;
using EveConv.Memory.Config;
using EveConv.Memory.Services;
using EveConv.Memory.Managers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace EveConv.Memory.Managers;

/// <summary>
/// Manages long-term memory entries — individual facts/habits/preferences/events
/// extracted across sessions via LLM and persisted to RDB via <see cref="ISqlSugarClient"/>.
/// </summary>
public sealed class LongMemory : ILongMemory
{
    private readonly LLMLongMemoryExtractor _extractor;
    private readonly ISessionMemory _session;
    private readonly ITokenCounter _tokenCounter;
    private readonly MemoryConfiguration _config;
    private readonly ISqlSugarClient _sqlClient;
    private readonly ILogger _logger;

    public LongMemory(
        LLMLongMemoryExtractor extractor,
        ISessionMemory session,
        ITokenCounter tokenCounter,
        IOptions<MemoryConfiguration> config,
        ISqlSugarClient sqlClient,
        ILoggerFactory? loggerFactory = null)
    {
        _extractor = extractor;
        _session = session;
        _tokenCounter = tokenCounter;
        _config = config.Value;
        _sqlClient = sqlClient;
        _logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<LongMemory>();
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

        // Load existing entries to pass to LLM for intelligent merge
        var existingEntities = await GetEntriesAsync(ownerKey);
        var existingEntries = existingEntities
            .Select(EntityMapper.ToDomain)
            .ToList();

        // Choose strategy: first extraction vs incremental merge
        IReadOnlyList<LongMemoryEntry> mergedEntries;
        if (existingEntries.Count == 0)
        {
            // No existing memories — simple extraction is faster and uses fewer tokens
            mergedEntries = await _extractor.ExtractAsync(compactionTexts, sessionIdList, ct);
        }
        else
        {
            // Existing memories — LLM merges old and new intelligently
            mergedEntries = await _extractor.MergeAsync(
                existingEntries, compactionTexts, sessionIdList, ct);
        }

        // Replace all entries atomically with the merged set
        await ReplaceAllEntriesAsync(ownerKey, mergedEntries);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Merged long memory for owner {OwnerKey}: {OldCount} existing + {NewSessions} sessions → {MergedCount} entries",
                ownerKey, existingEntries.Count, sessionIdList.Count, mergedEntries.Count);
        }
    }

    /// <inheritdoc />
    public Task UpsertAsync(string ownerKey, LongMemoryEntry entry, CancellationToken ct = default)
    {
        return UpsertInternalAsync(ownerKey, entry);
    }

    /// <inheritdoc />
    public async Task ForgetAsync(string ownerKey, string entryId, CancellationToken ct = default)
    {
        await _sqlClient.Deleteable<LongMemoryEntryEntity>()
            .Where(e => e.OwnerKey == ownerKey && e.Id == entryId)
            .ExecuteCommandAsync(ct);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Forgot long memory entry {EntryId} for owner {OwnerKey}", entryId, ownerKey);
        }
    }

    private async Task UpsertInternalAsync(string ownerKey, LongMemoryEntry entry)
    {
        // Find existing entry by owner + content
        var existing = await _sqlClient.Queryable<LongMemoryEntryEntity>()
            .Where(e => e.OwnerKey == ownerKey && e.Content == entry.Content)
            .FirstAsync();

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

            await _sqlClient.Updateable(existing)
                .WhereColumns(e => new { e.Id })
                .ExecuteCommandAsync();

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Reinforced existing long memory entry {EntryId}", existing.Id);
            }
        }
        else
        {
            // Add new entry
            var entity = EntityMapper.ToEntity(entry);
            entity.OwnerKey = ownerKey;

            await _sqlClient.Insertable(entity).ExecuteCommandAsync();

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Added new long memory entry {EntryId}", entry.Id);
            }
        }
    }

    private async Task<List<LongMemoryEntryEntity>> GetEntriesAsync(string ownerKey)
    {
        return await _sqlClient.Queryable<LongMemoryEntryEntity>()
            .Where(e => e.OwnerKey == ownerKey)
            .ToListAsync();
    }

    /// <summary>
    /// Atomically replaces all long memory entries for an owner with the given set.
    /// Used after LLM merge to ensure the DB reflects the complete merged state.
    /// </summary>
    private async Task ReplaceAllEntriesAsync(
        string ownerKey, IReadOnlyList<LongMemoryEntry> entries)
    {
        // Delete all existing entries for this owner
        await _sqlClient.Deleteable<LongMemoryEntryEntity>()
            .Where(e => e.OwnerKey == ownerKey)
            .ExecuteCommandAsync();

        // Bulk insert the merged set
        if (entries.Count > 0)
        {
            var entities = entries.Select(e =>
            {
                var entity = EntityMapper.ToEntity(e);
                entity.OwnerKey = ownerKey;
                return entity;
            }).ToList();

            await _sqlClient.Insertable(entities).ExecuteCommandAsync();
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Replaced all {Count} long memory entries for owner {OwnerKey}",
                entries.Count, ownerKey);
        }
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
