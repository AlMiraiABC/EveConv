using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.Memory;
using EveConv.Memory.Models;
using EveConv.Memory.Config;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scriban;

namespace EveConv.Memory.Services;

/// <summary>
/// Core session compaction logic implementing MEAI's <see cref="IChatReducer"/>.
/// When the model-facing history exceeds the context window, older messages are
/// compacted via LLM summarization. Supports multi-level compaction.
/// </summary>
public sealed class EveConvChatReducer : IChatReducer
{
    private static readonly Template SummarizationPromptTemplate = Template.Parse("""
        Summarize the following conversation history. Preserve key facts, decisions, and context.
        Keep the summary concise while retaining all essential information.

        --- Conversation History ---
        {{ for message in messages }}
        [{{ message.role }}]: {{ message.text }}
        {{ end }}
        --- End of History ---

        Provide a concise summary:
        """);

    private readonly IChatClient _summarizationClient;
    private readonly ISessionMemory _session;
    private readonly ITokenCounter _tokenCounter;
    private readonly MemoryConfiguration _config;
    private readonly ILogger _logger;

    /// <summary>
    /// Create an <see cref="EveConvChatReducer"/> instance.
    /// </summary>
    /// <param name="summarizationClient">LLM client to summarize histories.</param>
    /// <param name="session">Session memory to get all histories.</param>
    /// <param name="tokenCounter">Calculate context tokens.</param>
    /// <param name="options">Options to control.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    public EveConvChatReducer(
        [FromKeyedServices("summarization")] IChatClient summarizationClient,
        ISessionMemory session,
        ITokenCounter tokenCounter,
        IOptions<MemoryConfiguration> options,
        ILoggerFactory? loggerFactory = null)
    {
        _summarizationClient = summarizationClient;
        _session = session;
        _tokenCounter = tokenCounter;
        _config = options.Value;
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

        var sessionId = TryGetSessionId(messageList);
        if (sessionId is null)
        {
            return messageList;
        }

        // 1. Load existing compactions and build model-facing history
        var existingCompactions = await _session.GetCompactionsAsync(sessionId, ct);
        var coveredIds = BuildCoveredIdSet(existingCompactions);
        var uncovered = FilterUncovered(messageList, coveredIds);
        var modelHistory = BuildResultHistory(existingCompactions, uncovered);

        // 2. If within context window, return as-is
        var modelTokens = _tokenCounter.CountTokens(modelHistory);
        if (modelTokens <= _config.DefaultContextWindowTokens)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace(
                    "Session {SessionId} model history ({Tokens} tokens) within window — no compaction needed",
                    sessionId, modelTokens);
            }
            return modelHistory;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Session {SessionId} model history ({Tokens} tokens) exceeds window ({Window}) — compacting",
                sessionId, modelTokens, _config.DefaultContextWindowTokens);
        }

        // 3. Execute compaction pipeline
        return await ExecuteCompactionAsync(sessionId, uncovered, existingCompactions, modelHistory, ct);
    }

    private async Task<string> SummarizeMessagesAsync(
        IReadOnlyList<ChatMessage> messages, CancellationToken ct)
    {
        // Flatten messages to role/text pairs for the Scriban template
        var messageModels = messages
            .SelectMany(m => m.Contents
                .OfType<TextContent>()
                .Select(t => new
                {
                    role = m.Role.ToString().ToLowerInvariant(),
                    text = t.Text
                }))
            .ToList();
        try
        {
            var prompt = await SummarizationPromptTemplate.RenderAsync(new { messages = messageModels });
            var response = await _summarizationClient.GetResponseAsync(prompt, cancellationToken: ct);
            var summary = response.Text ?? string.Empty;
            if(_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Summarization produced {Length} chars", summary.Length);
            }
            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM summarization failed — falling back to truncation");
            // Fallback: return the last few messages as-is
            return "Summary unavailable. Recent conversation context follows.";
        }
    }

    /// <summary>
    /// Try to extract the session ID from the first message carrying <see cref="MemoryMetadataContent"/>.
    /// Returns <c>null</c> and logs a warning if no metadata is found on any message.
    /// </summary>
    private string? TryGetSessionId(IReadOnlyList<ChatMessage> messages)
    {
        var metadata = messages
            .Select(m => m.Contents.OfType<MemoryMetadataContent>().FirstOrDefault())
            .FirstOrDefault(m => m is not null);

        if (metadata is null)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "No MemoryMetadataContent found on messages — cannot determine session. Skipping compaction.");
            }
            return null;
        }

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Reducing {MessageCount} messages for session {SessionId}", messages.Count, metadata.SessionId);
        }

        return metadata.SessionId;
    }

    /// <summary>
    /// Build the set of message IDs that have already been compacted.
    /// </summary>
    private static HashSet<string> BuildCoveredIdSet(IReadOnlyList<SessionCompaction> compactions)
    {
        var covered = new HashSet<string>();
        foreach (var compaction in compactions)
        {
            foreach (var id in compaction.SourceMessageIds)
            {
                covered.Add(id);
            }
        }
        return covered;
    }

    /// <summary>
    /// Return messages whose IDs are not yet covered by any existing compaction.
    /// Messages without <see cref="MemoryMetadataContent"/> are treated as uncovered.
    /// </summary>
    private static List<ChatMessage> FilterUncovered(
        IReadOnlyList<ChatMessage> messages, HashSet<string> coveredIds)
    {
        var uncovered = new List<ChatMessage>();
        foreach (var message in messages)
        {
            var meta = message.Contents.OfType<MemoryMetadataContent>().FirstOrDefault();
            if (meta is null || !coveredIds.Contains(meta.MessageId))
            {
                uncovered.Add(message);
            }
        }
        return uncovered;
    }

    /// <summary>
    /// Build the model-facing history from existing compaction summaries and recent messages.
    /// When <paramref name="newSummary"/> is provided it is appended after the existing summaries.
    /// </summary>
    private static List<ChatMessage> BuildResultHistory(
        IReadOnlyList<SessionCompaction> compactions,
        IEnumerable<ChatMessage> recentMessages,
        string? newSummary = null)
    {
        var result = new List<ChatMessage>();
        foreach (var existing in compactions.OrderBy(c => c.CompactionLevel))
        {
            result.Add(new ChatMessage(ChatRole.System, existing.CompactedSummary));
        }
        if (newSummary is not null)
        {
            result.Add(new ChatMessage(ChatRole.System, newSummary));
        }
        result.AddRange(recentMessages);
        return result;
    }

    /// <summary>
    /// Split uncovered messages into to-compact and keep-recent buckets,
    /// and validate that compaction can proceed (has messages + within level cap).
    /// Returns <c>null</c> when compaction should be skipped.
    /// </summary>
    private (List<ChatMessage> ToCompact, List<ChatMessage> KeepRecent, int NewLevel)?
        TryPrepareCompaction(
            List<ChatMessage> uncovered,
            IReadOnlyList<SessionCompaction> existingCompactions)
    {
        var reserve = _config.CompactReserveRecentCount;
        var keepRecent = uncovered.Skip(Math.Max(0, uncovered.Count - reserve)).ToList();
        var toCompact = uncovered.Take(Math.Max(0, uncovered.Count - reserve)).ToList();

        if (toCompact.Count == 0)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Nothing to compact — all messages are within reserve count");
            }
            return null;
        }

        var maxLevel = existingCompactions.Count > 0
            ? existingCompactions.Max(c => c.CompactionLevel)
            : 0;
        var newLevel = maxLevel + 1;

        if (newLevel > _config.MaxCompactionLevel)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Session has reached max compaction level ({Max}) — skipping further compaction",
                    _config.MaxCompactionLevel);
            }
            return null;
        }

        return (toCompact, keepRecent, newLevel);
    }

    /// <summary>
    /// Execute the full compaction pipeline: prepare, summarize, persist, and assemble result.
    /// Falls back to <paramref name="modelHistory"/> when compaction cannot proceed.
    /// </summary>
    private async Task<IEnumerable<ChatMessage>> ExecuteCompactionAsync(
        string sessionId,
        List<ChatMessage> uncovered,
        IReadOnlyList<SessionCompaction> existingCompactions,
        List<ChatMessage> modelHistory,
        CancellationToken ct)
    {
        var prepared = TryPrepareCompaction(uncovered, existingCompactions);
        if (prepared is null)
        {
            return modelHistory;
        }

        var (toCompact, keepRecent, newLevel) = prepared.Value;

        var summaryText = await SummarizeMessagesAsync(toCompact, ct);
        var compactedTokens = _tokenCounter.CountTokens(summaryText);

        await PersistCompactionAsync(sessionId, toCompact, summaryText, compactedTokens, newLevel, ct);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Session {SessionId} compacted {CompactCount} messages → {SummaryTokens} tokens (level {Level})",
                sessionId, toCompact.Count, compactedTokens, newLevel);
        }

        return BuildResultHistory(existingCompactions, keepRecent, summaryText);
    }

    /// <summary>
    /// Create a <see cref="SessionCompaction"/> record and persist it to the session store.
    /// </summary>
    private async Task PersistCompactionAsync(
        string sessionId,
        IReadOnlyList<ChatMessage> toCompact,
        string summaryText,
        int compactedTokens,
        int newLevel,
        CancellationToken ct)
    {
        var sourceIds = toCompact
            .Select(m => m.Contents.OfType<MemoryMetadataContent>().FirstOrDefault()?.MessageId)
            .Where(id => id is not null)
            .Cast<string>()
            .ToList();

        var newCompaction = new SessionCompaction
        {
            Id = Guid.NewGuid().ToString("N"),
            SessionId = sessionId,
            CompactedSummary = summaryText,
            SourceMessageIds = sourceIds.AsReadOnly(),
            OriginalTokenCount = _tokenCounter.CountTokens(toCompact),
            CompactedTokenCount = compactedTokens,
            CompactionLevel = newLevel,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _session.SaveCompactionAsync(newCompaction, ct);
    }
}
