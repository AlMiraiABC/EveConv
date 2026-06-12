using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.Memory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace EveConv.Memory.Stores;

/// <summary>
/// In-memory implementation of <see cref="ISessionMemory"/> for development and testing.
/// Uses <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey, TValue}"/> for thread-safe storage.
/// </summary>
public sealed class InMemorySessionMemory : ISessionMemory
{
    private readonly Dictionary<string, List<ChatMessage>> _messages = [];
    private readonly Dictionary<string, ChatSession> _sessions = [];
    private readonly Dictionary<string, List<SessionCompaction>> _compactions = [];
    private readonly object _lock = new();

    /// <inheritdoc />
    public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        string sessionId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_messages.TryGetValue(sessionId, out var list))
            {
                return Task.FromResult<IReadOnlyList<ChatMessage>>(list.ToList().AsReadOnly());
            }
        }
        return Task.FromResult<IReadOnlyList<ChatMessage>>([]);
    }

    /// <inheritdoc />
    public Task<ChatMessage?> GetMessageByIdAsync(
        string messageId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            foreach (var (_, messages) in _messages)
            {
                foreach (var msg in messages)
                {
                    var meta = msg.Contents
                        .OfType<EveConv.Memory.Models.MemoryMetadataContent>()
                        .FirstOrDefault();
                    if (meta?.MessageId == messageId)
                    {
                        return Task.FromResult<ChatMessage?>(msg);
                    }
                }
            }
        }
        return Task.FromResult<ChatMessage?>(null);
    }

    /// <inheritdoc />
    public Task SaveMessagesAsync(
        string sessionId, IEnumerable<ChatMessage> messages, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_messages.TryGetValue(sessionId, out var list))
            {
                list = [];
                _messages[sessionId] = list;
            }

            foreach (var message in messages)
            {
                var meta = message.Contents
                    .OfType<EveConv.Memory.Models.MemoryMetadataContent>()
                    .FirstOrDefault();

                // Upsert: replace existing message with same ID
                if (meta is not null)
                {
                    var existingIndex = list.FindIndex(m =>
                        m.Contents.OfType<EveConv.Memory.Models.MemoryMetadataContent>()
                            .FirstOrDefault()?.MessageId == meta.MessageId);

                    if (existingIndex >= 0)
                    {
                        list[existingIndex] = message;
                        continue;
                    }
                }

                list.Add(message);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SessionCompaction>> GetCompactionsAsync(
        string sessionId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_compactions.TryGetValue(sessionId, out var list))
            {
                return Task.FromResult<IReadOnlyList<SessionCompaction>>(
                    list.OrderBy(c => c.CompactionLevel).ToList().AsReadOnly());
            }
        }
        return Task.FromResult<IReadOnlyList<SessionCompaction>>([]);
    }

    /// <inheritdoc />
    public Task SaveCompactionAsync(
        SessionCompaction compaction, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_compactions.TryGetValue(compaction.SessionId, out var list))
            {
                list = [];
                _compactions[compaction.SessionId] = list;
            }

            list.Add(compaction);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ChatSession?> GetSessionAsync(
        string sessionId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                return Task.FromResult<ChatSession?>(session);
            }
        }
        return Task.FromResult<ChatSession?>(null);
    }

    /// <inheritdoc />
    public Task SaveSessionAsync(
        ChatSession session, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _sessions[session.SessionId] = session;
        }
        return Task.CompletedTask;
    }
}
