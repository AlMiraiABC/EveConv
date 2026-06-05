using SqlSugar;

namespace EveConv.Memory.Models;

/// <summary>
/// SqlSugar entity for chat session metadata.
/// Maps to the <c>chat_sessions</c> table.
/// </summary>
[SugarTable("chat_sessions")]
public sealed class ChatSessionEntity
{
    [SugarColumn(IsPrimaryKey = true, ColumnDataType = "varchar(64)")]
    public string Id { get; set; } = string.Empty;

    [SugarColumn(IsNullable = false)]
    public DateTime CreatedAt { get; set; }

    public DateTime LastActiveAt { get; set; }

    public int TotalMessageCount { get; set; }

    public int TotalTokenCount { get; set; }
}
