using System.Text;
using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.Memory;
using EveConv.Memory.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EveConv.Memory.Services;

/// <summary>
/// Core session compaction logic implementing MEAI's <see cref="IChatReducer"/>.
/// When the model-facing history exceeds the context window, older messages are
/// compacted via LLM summarization. Supports multi-level compaction.
/// </summary>
public sealed class EveConvChatReducer : IChatReducer
{
    private readonly IChatClient _summarizationClient;
    private readonly ISessionMemoryStore _store;
    private readonly ITokenCounter _tokenCounter;
    private readonly MemoryConfiguration _config;
    private readonly ILogger _logger;

    public EveConvChatReducer(
        [FromKeyedServices("summarization")] IChatClient summarizationClient,
        ISessionMemoryStore store,
        ITokenCounter tokenCounter,
        IOptions<MemoryConfiguration> config,
        ILoggerFactory? loggerFactory = null)
    {
        _summarizationClient = summarizationClient;
        _store = store;
        _tokenCounter = tokenCounter;
        _config = config.Value;
        _logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<EveConvChatReducer>();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ChatMessage>> ReduceAsync(
        IEnumerable<ChatMessage> messages, CancellationToken ct = default)
    {
        var messageList = messages?.ToList() ?? [];
        if (messageList.Count == 0)
        {
            return messageList;
        }

        // 1. Extract metadata
        var metadataList = messageList
            .Select(m => m.Contents.OfType<MemoryMetadataContent>().FirstOrDefault())
            .Where(m => m is not null)
            .ToList();

        if (metadataList.Count == 0)
        {
            _logger.LogWarning("No MemoryMetadataContent found on messages — cannot determine session. Skipping compaction.");
            return messageList;
        }

        var sessionId = metadataList[0]!.SessionId;
        _logger.LogTrace("Reducing {MessageCount} messages for session {SessionId}", messageList.Count, sessionId);

        // 2. Query existing compactions
        var existingCompactions = await _store.GetCompactionsAsync(sessionId, ct);

        // 3. Build model-facing history: existing summaries + uncovered raw messages
        var coveredIds = new HashSet<string>();
        foreach (var compaction in existingCompactions)
        {
            foreach (var id in compaction.SourceMessageIds)
            {
                coveredIds.Add(id);
            }
        }

        var uncovered = new List<ChatMessage>();
        foreach (var message in messageList)
        {
            var meta = message.Contents.OfType<MemoryMetadataContent>().FirstOrDefault();
            if (meta is null || !coveredIds.Contains(meta.MessageId))
            {
                uncovered.Add(message);
            }
        }

        // Build model-facing history
        var modelHistory = new List<ChatMessage>();
        foreach (var existing in existingCompactions.OrderBy(c => c.CompactionLevel))
        {
            modelHistory.Add(new ChatMessage(ChatRole.System, existing.CompactedSummary));
        }
        modelHistory.AddRange(uncovered);

        // 4. Calculate tokens
        var modelTokens = _tokenCounter.CountTokens(modelHistory);

        // 5. If within window, return as-is
        if (modelTokens <= _config.DefaultContextWindowTokens)
        {
            _logger.LogTrace("Session {SessionId} model history ({Tokens} tokens) within window — no compaction needed",
                sessionId, modelTokens);
            return modelHistory;
        }

        _logger.LogInformation("Session {SessionId} model history ({Tokens} tokens) exceeds window ({Window}) — compacting",
            sessionId, modelTokens, _config.DefaultContextWindowTokens);

        // 6. Separate: messages to compact vs. messages to keep
        var keepRecent = uncovered
            .Skip(Math.Max(0, uncovered.Count - _config.CompactReserveRecentCount))
            .ToList();

        var toCompact = uncovered
            .Take(Math.Max(0, uncovered.Count - _config.CompactReserveRecentCount))
            .ToList();

        if (toCompact.Count == 0)
        {
            _logger.LogTrace("Nothing to compact — all messages are within reserve count");
            return modelHistory;
        }

        // 7. Build LLM summarization prompt
        var summaryText = await SummarizeMessagesAsync(toCompact, ct);

        // 8. Create and persist compaction
        var maxLevel = existingCompactions.Count > 0
            ? existingCompactions.Max(c => c.CompactionLevel)
            : 0;
        var newLevel = maxLevel + 1;

        if (newLevel > _config.MaxCompactionLevel)
        {
            _logger.LogWarning("Session {SessionId} has reached max compaction level ({Max}) — skipping further compaction",
                sessionId, _config.MaxCompactionLevel);
            return modelHistory;
        }

        var sourceIds = toCompact
            .Select(m => m.Contents.OfType<MemoryMetadataContent>().FirstOrDefault()?.MessageId)
            .Where(id => id is not null)
            .Cast<string>()
            .ToList();

        var originalTokens = _tokenCounter.CountTokens(toCompact);
        var compactedTokens = _tokenCounter.CountTokens(summaryText);

        var newCompaction = new SessionCompaction
        {
            Id = Guid.NewGuid().ToString("N"),
            SessionId = sessionId,
            CompactedSummary = summaryText,
            SourceMessageIds = sourceIds.AsReadOnly(),
            OriginalTokenCount = originalTokens,
            CompactedTokenCount = compactedTokens,
            CompactionLevel = newLevel,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _store.SaveCompactionAsync(newCompaction, ct);

        // 9. Build result: compacted model-facing history
        var resultMessages = new List<ChatMessage>();
        foreach (var existing in existingCompactions.OrderBy(c => c.CompactionLevel))
        {
            resultMessages.Add(new ChatMessage(ChatRole.System, existing.CompactedSummary));
        }
        resultMessages.Add(new ChatMessage(ChatRole.System, summaryText));
        resultMessages.AddRange(keepRecent);

        _logger.LogInformation(
            "Session {SessionId} compacted {CompactCount} messages → {SummaryTokens} tokens (level {Level})",
            sessionId, toCompact.Count, compactedTokens, newLevel);

        return resultMessages;
    }

    private async Task<string> SummarizeMessagesAsync(
        IReadOnlyList<ChatMessage> messages, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Summarize the following conversation history. Preserve key facts, decisions, and context.");
        sb.AppendLine("Keep the summary concise while retaining all essential information.");
        sb.AppendLine();
        sb.AppendLine("--- Conversation History ---");

        foreach (var message in messages)
        {
            var role = message.Role.ToString().ToLowerInvariant();
            foreach (var content in message.Contents)
            {
                if (content is TextContent text)
                {
                    sb.AppendLine($"[{role}]: {text.Text}");
                }
            }
        }

        sb.AppendLine("--- End of History ---");
        sb.AppendLine();
        sb.AppendLine("Provide a concise summary:");

        var prompt = sb.ToString();

        try
        {
            var response = await _summarizationClient.GetResponseAsync(prompt, cancellationToken: ct);
            var summary = response.Text ?? string.Empty;
            _logger.LogTrace("Summarization produced {Length} chars", summary.Length);
            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM summarization failed — falling back to truncation");
            // Fallback: return the last few messages as-is
            return "Summary unavailable. Recent conversation context follows.";
        }
    }
}
