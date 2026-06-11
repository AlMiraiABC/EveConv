using System.Text;
using System.Text.Json;
using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.Memory;
using EveConv.Memory.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EveConv.Memory.Services;

/// <summary>
/// Uses an LLM to extract long-term memory entries (preferences, habits, events, facts)
/// from session history compactions.
/// </summary>
public sealed class LLMLongMemoryExtractor
{
    private readonly IChatClient _extractionClient;
    private readonly MemoryOptions _config;
    private readonly ILogger _logger;

    public LLMLongMemoryExtractor(
        [FromKeyedServices("extraction")] IChatClient extractionClient,
        IOptions<MemoryOptions> config,
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

        var prompt = BuildExtractionPrompt(sessionContents);

        try
        {
            var response = await _extractionClient.GetResponseAsync(prompt, cancellationToken: ct);
            var responseText = response.Text ?? string.Empty;

            _logger.LogTrace("LLM extraction response: {Length} chars", responseText.Length);

            return ParseExtractionResponse(responseText, sourceSessionIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM long memory extraction failed");
            return [];
        }
    }

    private string BuildExtractionPrompt(IReadOnlyList<string> sessionContents)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Analyze the following conversation history and extract:");
        sb.AppendLine("1. User preferences (likes, dislikes, preferred styles)");
        sb.AppendLine("2. User habits (recurring behaviors, patterns)");
        sb.AppendLine("3. Important events (life events, milestones)");
        sb.AppendLine("4. Facts about the user (name, role, skills, background)");
        sb.AppendLine();
        sb.AppendLine("For each extracted item, output a JSON object with:");
        sb.AppendLine("- category: one of \"preference\", \"habit\", \"event\", \"fact\"");
        sb.AppendLine("- content: a short description (under 50 words)");
        sb.AppendLine("- importance: a float from 0.0 to 1.0");
        sb.AppendLine();
        sb.AppendLine($"Only include items with importance > {_config.ImportanceThreshold}.");
        sb.AppendLine("Output as a JSON array. Example format:");
        sb.AppendLine("[{\"category\":\"preference\",\"content\":\"Likes Python\",\"importance\":0.8}]");
        sb.AppendLine();
        sb.AppendLine("--- Conversation History ---");
        foreach (var content in sessionContents)
        {
            sb.AppendLine(content);
            sb.AppendLine("---");
        }
        sb.AppendLine("--- End of History ---");
        sb.AppendLine();
        sb.AppendLine("Extracted entries (JSON array):");

        return sb.ToString();
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
                _logger.LogWarning("Could not find JSON array in extraction response");
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

            _logger.LogTrace("Parsed {Count} entries from extraction response", entries.Count);
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
        public string Category { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public float Importance { get; set; }
    }
}
