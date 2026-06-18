using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.Memory;
using EveConv.Memory.Config;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scriban;

namespace EveConv.Memory.Services;

/// <summary>
/// Uses an LLM to extract long-term memory entries (preferences, habits, events, facts)
/// from session history compactions.
/// </summary>
public sealed class LLMLongMemoryExtractor
{
    private static readonly Template ExtractionPromptTemplate = Template.Parse(
        """
        Analyze the following conversation history and extract:
        1. User preferences (likes, dislikes, preferred styles)
        2. User habits (recurring behaviors, patterns)
        3. Important events (life events, milestones)
        4. Facts about the user (name, role, skills, background)
        
        For each extracted item, output a JSON object with:
        - category: one of "preference", "habit", "event", "fact"
        - content: a short description (under 50 words)
        - importance: a float from 0.0 to 1.0
        
        Only include items with importance > {{ importance_threshold }}.
        Output as a JSON array. Example format:
        [{"category":"preference","content":"Likes Python","importance":0.8}]
        
        --- Conversation History ---
        {{ for content in session_contents }}
        {{ content }}
        ---
        {{ end }}
        --- End of History ---
        
        Extracted entries (JSON array):
        """);

    private static readonly Template MergePromptTemplate = Template.Parse(
        """
        You maintain a long-term memory for a user. Your task is to produce the COMPLETE
        updated memory list by incorporating new conversations into existing memories.
        
        Rules:
        - KEEP entries that are still valid (include them in output as-is, or update them)
        - UPDATE an entry if new information revises it (change content, adjust importance)
        - DELETE an entry by omitting it from output (stale, contradicted, or no longer relevant)
        - ADD new entries from new conversations when you discover important facts
        - MERGE semantically similar entries into a single entry with higher importance
        
        Output ONLY a JSON array. Each object has:
        - category: "preference", "habit", "event", or "fact"
        - content: a short description (under 50 words)
        - importance: a float from 0.0 to 1.0 (higher = more confident/important)
        
        Output as a JSON array. Example format:
        [{"category":"preference","content":"Likes Python","importance":0.8}]

        Only include items with importance > {{ importance_threshold }}.
        
        --- EXISTING MEMORIES ---
        {{ if existing_entries | array.size > 0 }}
        {{ for entry in existing_entries }}
        [{{ entry.category }} | importance: {{ entry.importance }}]
        {{ entry.content }}
        ---
        {{ end }}
        {{ else }}
        (no existing memories yet)
        {{ end }}
        
        --- NEW CONVERSATIONS ---
        {{ for content in session_contents }}
        {{ content }}
        ---
        {{ end }}
        
        Updated memory list (JSON array):
        """);

    private readonly IChatClient _extractionClient;
    private readonly MemoryConfiguration _config;
    private readonly ILogger _logger;

    public LLMLongMemoryExtractor(
        [FromKeyedServices("extraction")] IChatClient extractionClient,
        IOptions<MemoryConfiguration> config,
        ILoggerFactory? loggerFactory = null)
    {
        _extractionClient = extractionClient;
        _config = config.Value;
        _logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<LLMLongMemoryExtractor>();
    }

    /// <summary>
    /// Extracts long-term memory entries from the given session content texts.
    /// </summary>
    /// <param name="sessionContents">Session compaction or message texts to analyze.</param>
    /// <param name="sourceSessionIds">The session IDs being analyzed.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A list of extracted <see cref="LongMemoryEntry"/> instances.</returns>
    public async Task<IReadOnlyList<LongMemoryEntry>> ExtractAsync(
        IReadOnlyList<string> sessionContents,
        IReadOnlyList<string> sourceSessionIds,
        CancellationToken ct = default)
    {
        if (sessionContents is null || sessionContents.Count == 0)
        {
            return [];
        }
        try
        {
            var prompt = await ExtractionPromptTemplate.RenderAsync(new
            {
                importance_threshold = _config.ImportanceThreshold,
                session_contents = sessionContents
            });
            var response = await _extractionClient.GetResponseAsync(prompt, new()
            {
                ResponseFormat = ChatResponseFormat.Json,
            }, ct);
            var responseText = response.Text ?? string.Empty;
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("LLM extraction response: {Length} chars", responseText.Length);
            }
            return ParseExtractionResponse(responseText, sourceSessionIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM long memory extraction failed");
            return [];
        }
    }

    /// <summary>
    /// Merges new session content into an existing set of long-term memory entries.
    /// The LLM receives both existing memories and new conversations, and outputs
    /// the complete updated list — handling updates, deletions, and additions.
    /// </summary>
    /// <param name="existingEntries">Current long-term memory entries for the owner.</param>
    /// <param name="sessionContents">Session compaction or message texts to analyze.</param>
    /// <param name="sourceSessionIds">The session IDs being analyzed.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The complete merged list of <see cref="LongMemoryEntry"/> instances.</returns>
    public async Task<IReadOnlyList<LongMemoryEntry>> MergeAsync(
        IReadOnlyList<LongMemoryEntry> existingEntries,
        IReadOnlyList<string> sessionContents,
        IReadOnlyList<string> sourceSessionIds,
        CancellationToken ct = default)
    {
        if (sessionContents is null || sessionContents.Count == 0)
        {
            // No new content — return existing entries unchanged
            return existingEntries;
        }

        try
        {
            var existingViews = existingEntries
                .Select(e => new { e.Category, e.Content, e.Importance })
                .ToList();

            var prompt = await MergePromptTemplate.RenderAsync(new
            {
                importance_threshold = _config.ImportanceThreshold,
                existing_entries = existingViews,
                session_contents = sessionContents
            });

            var response = await _extractionClient.GetResponseAsync(prompt, new()
            {
                ResponseFormat = ChatResponseFormat.Json,
            }, ct);
            var responseText = response.Text ?? string.Empty;

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("LLM merge response: {Length} chars", responseText.Length);
            }

            // If the LLM response doesn't contain a JSON array at all,
            // the merge failed — keep existing entries unchanged.
            if (!responseText.Contains('[') || !responseText.Contains(']'))
            {
                _logger.LogWarning("LLM merge response contained no JSON array — keeping existing entries");
                return existingEntries;
            }

            var merged = ParseExtractionResponse(responseText, sourceSessionIds);
            if (merged.Count == 0)
            {
                // LLM returned empty array — interpret as "delete all memories"
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("LLM merge returned empty list — all memories cleared");
                }
            }

            return merged;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM long memory merge failed — returning existing entries unchanged");
            return existingEntries;
        }
    }

    private IReadOnlyList<LongMemoryEntry> ParseExtractionResponse(
        string responseText, IReadOnlyList<string> sourceSessionIds)
    {
        try
        {
            // Try to extract JSON array from response
            var jsonStart = responseText.IndexOf('[');
            var jsonEnd = responseText.LastIndexOf(']');

            if (jsonStart < 0 || jsonEnd < 0 || jsonEnd <= jsonStart)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Could not find JSON array in extraction response");
                }
                return [];
            }

            var json = responseText[jsonStart..(jsonEnd + 1)];

            var rawEntries = JsonSerializer.Deserialize<List<RawExtractedEntry>>(json);

            if (rawEntries is null || rawEntries.Count == 0)
            {
                return [];
            }

            var now = DateTimeOffset.UtcNow;
            var entries = new List<LongMemoryEntry>();

            foreach (var raw in rawEntries)
            {
                if (string.IsNullOrWhiteSpace(raw.Category) ||
                    string.IsNullOrWhiteSpace(raw.Content) ||
                    raw.Importance < _config.ImportanceThreshold)
                {
                    continue;
                }

                entries.Add(new LongMemoryEntry
                {
                    Id = Guid.NewGuid().ToString("N"),
                    OwnerKey = "default", // Will be overridden by manager
                    Category = raw.Category.ToLowerInvariant(),
                    Content = raw.Content.Trim(),
                    SourceSessionIds = sourceSessionIds,
                    Importance = raw.Importance,
                    CreatedAt = now,
                    LastReinforcedAt = now
                });
            }
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Parsed {Count} entries from extraction response", entries.Count);
            }
            return entries.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse extraction response JSON");
            return [];
        }
    }

    private sealed class RawExtractedEntry
    {
        [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;
        [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
        [JsonPropertyName("importance")] public float Importance { get; set; }
    }
}
