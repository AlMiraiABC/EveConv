using SqlSugar;

namespace EveConv.Memory.Models;

/// <summary>
/// SqlSugar entity for individual chat messages.
/// Maps to the <c>chat_messages</c> table.
/// The full <see cref="Microsoft.Extensions.AI.ChatMessage"/> is serialized as JSON in <see cref="Content"/>.
/// </summary>
[SugarTable("chat_messages")]
public sealed class ChatMessageEntity
{
    [SugarColumn(IsPrimaryKey = true, ColumnDataType = "varchar(64)")]
    public string Id { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "varchar(64)")]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// The chat role as a string: user, assistant, or system.
    /// Used for SQL-level filtering.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Serialized <see cref="Microsoft.Extensions.AI.ChatMessage"/> JSON.
    /// </summary>
    [SugarColumn(ColumnDataType = "text")]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Monotonically increasing order within the session.
    /// </summary>
    public int SequenceNumber { get; set; }
}
