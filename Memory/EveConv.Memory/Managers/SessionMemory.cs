using System.Text.Json;
using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.Memory;
using EveConv.Memory.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace EveConv.Memory.Managers;

/// <summary>
/// RDB-backed implementation of <see cref="ISessionMemory"/> using <see cref="ISqlSugarClient"/>.
/// Handles entity-to-domain mapping and persistence for chat messages, sessions, and compactions.
/// </summary>
public sealed class SessionMemory : ISessionMemory
{
    private readonly ISqlSugarClient _sqlClient;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="SessionMemory"/>.
    /// </summary>
    /// <param name="sqlClient">The SqlSugar client (registered by the startup project).</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    public SessionMemory(
        ISqlSugarClient sqlClient,
        ILoggerFactory? loggerFactory = null)
    {
        _sqlClient = sqlClient;
        _logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<SessionMemory>();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        string sessionId, CancellationToken ct = default)
    {
        var entities = await _sqlClient.Queryable<ChatMessageEntity>()
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.SequenceNumber)
            .ToListAsync(ct);

        return entities.Select(EntityMapper.ToChatMessage).ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<ChatMessage?> GetMessageByIdAsync(
        string messageId, CancellationToken ct = default)
    {
        var entity = await _sqlClient.Queryable<ChatMessageEntity>()
            .Where(m => m.Id == messageId)
            .FirstAsync(ct);

        return entity is not null ? EntityMapper.ToChatMessage(entity) : null;
    }

    /// <inheritdoc />
    public async Task SaveMessagesAsync(
        string sessionId, IEnumerable<ChatMessage> messages, CancellationToken ct = default)
    {
        var entities = messages
            .Select((m, i) => EntityMapper.ToEntity(m, sessionId, i))
            .ToList();

        if (entities.Count == 0) return;

        // Use Storageable for upsert (insert or update)
        var storage = _sqlClient.Storageable(entities)
            .WhereColumns(it => new { it.Id })
            .ToStorage();

        await storage.AsInsertable.ExecuteCommandAsync(ct);
        await storage.AsUpdateable.ExecuteCommandAsync(ct);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Saved {Count} messages to session {SessionId}", entities.Count, sessionId);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SessionCompaction>> GetCompactionsAsync(
        string sessionId, CancellationToken ct = default)
    {
        var entities = await _sqlClient.Queryable<SessionCompactionEntity>()
            .Where(c => c.SessionId == sessionId)
            .OrderBy(c => c.CompactionLevel)
            .ToListAsync(ct);

        return entities.Select(EntityMapper.ToDomain).ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public async Task SaveCompactionAsync(
        SessionCompaction compaction, CancellationToken ct = default)
    {
        var entity = EntityMapper.ToEntity(compaction);
        await _sqlClient.Insertable(entity).ExecuteCommandAsync(ct);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Saved compaction {CompactionId} for session {SessionId}",
                compaction.Id, compaction.SessionId);
        }
    }

    /// <inheritdoc />
    public async Task<ChatSession?> GetSessionAsync(
        string sessionId, CancellationToken ct = default)
    {
        var entity = await _sqlClient.Queryable<ChatSessionEntity>()
            .Where(s => s.Id == sessionId)
            .FirstAsync(ct);

        return entity is not null ? EntityMapper.ToDomain(entity) : null;
    }

    /// <inheritdoc />
    public async Task SaveSessionAsync(
        ChatSession session, CancellationToken ct = default)
    {
        var entity = EntityMapper.ToEntity(session);

        var storage = _sqlClient.Storageable(entity)
            .WhereColumns(it => new { it.Id })
            .ToStorage();

        await storage.AsInsertable.ExecuteCommandAsync(ct);
        await storage.AsUpdateable.ExecuteCommandAsync(ct);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Saved session {SessionId}", session.SessionId);
        }
    }
}
