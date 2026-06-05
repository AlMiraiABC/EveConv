using SqlSugar;

namespace EveConv.Memory.Models;

/// <summary>
/// SqlSugar entity for long-term memory entries.
/// Maps to the <c>long_memory_entries</c> table.
/// </summary>
[SugarTable("long_memory_entries")]
public sealed class LongMemoryEntryEntity
{
    [SugarColumn(IsPrimaryKey = true, ColumnDataType = "varchar(64)")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Opaque partition key. Defaults to "default" for single-user scenarios.
    /// </summary>
    [SugarColumn(ColumnDataType = "varchar(128)")]
    public string OwnerKey { get; set; } = "default";

    /// <summary>
    /// Memory category: preference, habit, event, or fact.
    /// </summary>
    [SugarColumn(ColumnDataType = "varchar(32)")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// The memory entry content text.
    /// </summary>
    [SugarColumn(ColumnDataType = "text")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of source session IDs from which this entry was extracted.
    /// </summary>
    [SugarColumn(ColumnDataType = "text")]
    public string SourceSessionIdsJson { get; set; } = "[]";

    public float Importance { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastReinforcedAt { get; set; }
}
