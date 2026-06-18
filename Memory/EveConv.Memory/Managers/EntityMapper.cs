using System.Text.Json;
using EveConv.Abstraction.Memory;
using EveConv.Memory.Models;
using Microsoft.Extensions.AI;

namespace EveConv.Memory.Managers;

/// <summary>
/// Static helper for converting between SqlSugar persistence entities
/// and abstraction layer contract models.
/// </summary>
public static class EntityMapper
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    #region ChatMessage ↔ ChatMessageEntity

    /// <summary>
    /// Converts a <see cref="ChatMessageEntity"/> to a MEAI <see cref="ChatMessage"/>.
    /// Deserializes the stored JSON and reconstructs <see cref="MemoryMetadataContent"/>.
    /// </summary>
    /// <seealso cref="ToEntity(ChatMessage, string, int)"/>
    public static ChatMessage ToChatMessage(ChatMessageEntity entity)
    {
        // Deserialize the stored ChatMessage JSON
        ChatMessage message;
        try
        {
            message = JsonSerializer.Deserialize<ChatMessage>(entity.Content, _jsonOptions)
                ?? new ChatMessage(ChatRole.User, "");
        }
        catch
        {
            message = new ChatMessage(ChatRole.User, entity.Content);
        }

        // Reconstruct MemoryMetadataContent
        var existingMeta = message.Contents.OfType<MemoryMetadataContent>().FirstOrDefault();
        if (existingMeta is not null)
        {
            // Update with entity-level data
            message.Contents.Remove(existingMeta);
        }

        message.Contents.Add(new MemoryMetadataContent
        {
            MessageId = entity.Id,
            SessionId = entity.SessionId
        });

        return message;
    }

    /// <summary>
    /// Converts a MEAI <see cref="ChatMessage"/> to a <see cref="ChatMessageEntity"/> for persistence.
    /// Extracts <see cref="MemoryMetadataContent"/> for message ID and session ID.
    /// </summary>
    /// <seealso cref="ToChatMessage"/>
    public static ChatMessageEntity ToEntity(
        ChatMessage message, string sessionId, int sequenceNumber)
    {
        var meta = message.Contents.OfType<MemoryMetadataContent>().FirstOrDefault();

        // Serialize the ChatMessage to JSON without MemoryMetadataContent.
        // It is re-attached from entity fields during deserialization
        // that stored separately.
        var cleanMessage = new ChatMessage(
            message.Role,
            message.Contents.Where(c => c is not MemoryMetadataContent).ToArray());
        var json = JsonSerializer.Serialize(cleanMessage, _jsonOptions);

        return new ChatMessageEntity
        {
            Id = meta?.MessageId ?? Guid.NewGuid().ToString("N"),
            SessionId = meta?.SessionId ?? sessionId,
            Role = message.Role.ToString().ToLowerInvariant(),
            Content = json,
            CreatedAt = DateTime.UtcNow,
            SequenceNumber = sequenceNumber
        };
    }

    #endregion

    #region ChatSession ↔ ChatSessionEntity

    /// <summary>
    /// Converts a <see cref="ChatSessionEntity"/> to a domain <see cref="ChatSession"/>.
    /// </summary>
    public static ChatSession ToDomain(ChatSessionEntity entity)
    {
        return new ChatSession
        {
            SessionId = entity.Id,
            CreatedAt = entity.CreatedAt,
            LastActiveAt = entity.LastActiveAt,
            TotalMessageCount = entity.TotalMessageCount,
            TotalTokenCount = entity.TotalTokenCount
        };
    }

    /// <summary>
    /// Converts a domain <see cref="ChatSession"/> to a <see cref="ChatSessionEntity"/>.
    /// </summary>
    public static ChatSessionEntity ToEntity(ChatSession session)
    {
        return new ChatSessionEntity
        {
            Id = session.SessionId,
            CreatedAt = session.CreatedAt.UtcDateTime,
            LastActiveAt = session.LastActiveAt.UtcDateTime,
            TotalMessageCount = session.TotalMessageCount,
            TotalTokenCount = session.TotalTokenCount
        };
    }

    #endregion

    #region SessionCompaction ↔ SessionCompactionEntity

    /// <summary>
    /// Converts a <see cref="SessionCompactionEntity"/> to a domain <see cref="SessionCompaction"/>.
    /// </summary>
    public static SessionCompaction ToDomain(SessionCompactionEntity entity)
    {
        return new SessionCompaction
        {
            Id = entity.Id,
            SessionId = entity.SessionId,
            CompactedSummary = entity.CompactedSummary,
            SourceMessageIds = DeserializeStringList(entity.SourceMessageIdsJson),
            OriginalTokenCount = entity.OriginalTokenCount,
            CompactedTokenCount = entity.CompactedTokenCount,
            CompactionLevel = entity.CompactionLevel,
            CreatedAt = entity.CreatedAt
        };
    }

    /// <summary>
    /// Converts a domain <see cref="SessionCompaction"/> to a <see cref="SessionCompactionEntity"/>.
    /// </summary>
    public static SessionCompactionEntity ToEntity(SessionCompaction compaction)
    {
        return new SessionCompactionEntity
        {
            Id = compaction.Id,
            SessionId = compaction.SessionId,
            CompactedSummary = compaction.CompactedSummary,
            SourceMessageIdsJson = SerializeStringList(compaction.SourceMessageIds),
            OriginalTokenCount = compaction.OriginalTokenCount,
            CompactedTokenCount = compaction.CompactedTokenCount,
            CompactionLevel = compaction.CompactionLevel,
            CreatedAt = compaction.CreatedAt.UtcDateTime
        };
    }

    #endregion

    #region LongMemoryEntry ↔ LongMemoryEntryEntity

    /// <summary>
    /// Converts a <see cref="LongMemoryEntryEntity"/> to a domain <see cref="LongMemoryEntry"/>.
    /// </summary>
    public static LongMemoryEntry ToDomain(LongMemoryEntryEntity entity)
    {
        return new LongMemoryEntry
        {
            Id = entity.Id,
            OwnerKey = entity.OwnerKey,
            Category = entity.Category,
            Content = entity.Content,
            SourceSessionIds = DeserializeStringList(entity.SourceSessionIdsJson),
            Importance = entity.Importance,
            CreatedAt = entity.CreatedAt,
            LastReinforcedAt = entity.LastReinforcedAt
        };
    }

    /// <summary>
    /// Converts a domain <see cref="LongMemoryEntry"/> to a <see cref="LongMemoryEntryEntity"/>.
    /// </summary>
    public static LongMemoryEntryEntity ToEntity(LongMemoryEntry entry)
    {
        return new LongMemoryEntryEntity
        {
            Id = entry.Id,
            OwnerKey = entry.OwnerKey,
            Category = entry.Category,
            Content = entry.Content,
            SourceSessionIdsJson = SerializeStringList(entry.SourceSessionIds),
            Importance = entry.Importance,
            CreatedAt = entry.CreatedAt.UtcDateTime,
            LastReinforcedAt = entry.LastReinforcedAt.UtcDateTime
        };
    }

    #endregion

    #region helpers

    private static string SerializeStringList(IEnumerable<string> items)
    {
        return JsonSerializer.Serialize(items.ToList(), _jsonOptions);
    }

    private static IReadOnlyList<string> DeserializeStringList(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, _jsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    #endregion
}
