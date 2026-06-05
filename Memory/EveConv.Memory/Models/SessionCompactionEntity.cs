using SqlSugar;

namespace EveConv.Memory.Models;

/// <summary>
/// SqlSugar entity for session compaction records.
/// Maps to the <c>session_compactions</c> table.
/// </summary>
[SugarTable("session_compactions")]
public sealed class SessionCompactionEntity
{
    [SugarColumn(IsPrimaryKey = true, ColumnDataType = "varchar(64)")]
    public string Id { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "varchar(64)")]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// The LLM-generated summary text.
    /// </summary>
    [SugarColumn(ColumnDataType = "text")]
    public string CompactedSummary { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of source message IDs that were compacted.
    /// </summary>
    [SugarColumn(ColumnDataType = "text")]
    public string SourceMessageIdsJson { get; set; } = "[]";

    public int OriginalTokenCount { get; set; }

    public int CompactedTokenCount { get; set; }

    public int CompactionLevel { get; set; }

    public DateTime CreatedAt { get; set; }
}
