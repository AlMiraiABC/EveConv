using EveConv.Abstraction.Memory;
using EveConv.Memory.Managers;
using EveConv.Memory.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;

namespace EveConv.Memory.Tests.Managers;

/// <summary>
/// Unit tests for <see cref="SessionMemory"/> using SQLite in-memory database.
/// Each test gets a fresh database instance.
/// </summary>
public class SessionMemoryTests
{
    private const string DefaultSessionId = "session-1";
    private const string OtherSessionId = "session-2";

    #region helpers

    /// <summary>
    /// Creates an in-memory SQLite <see cref="ISqlSugarClient"/> with all
    /// SessionMemory tables pre-created.
    /// </summary>
    private static SqlSugarScope CreateSqliteClient()
    {
        var client = new SqlSugarScope(new ConnectionConfig
        {
            DbType = DbType.Sqlite,
            ConnectionString = "DataSource=:memory:",
            IsAutoCloseConnection = false
        });

        client.DbMaintenance.CreateDatabase();
        client.CodeFirst.InitTables<ChatMessageEntity>();
        client.CodeFirst.InitTables<SessionCompactionEntity>();
        client.CodeFirst.InitTables<ChatSessionEntity>();

        return client;
    }

    private static SessionMemory CreateSut(ISqlSugarClient? sqlClient = null)
    {
        return new SessionMemory(
            sqlClient ?? CreateSqliteClient(),
            NullLoggerFactory.Instance);
    }

    /// <summary>
    /// Creates a <see cref="ChatMessage"/> with minimal required data.
    /// </summary>
    private static ChatMessage CreateMessage(string text, string? messageId = null, string? sessionId = null)
    {
        var message = new ChatMessage(ChatRole.User, text);
        message.Contents.Add(new MemoryMetadataContent
        {
            MessageId = messageId ?? Guid.NewGuid().ToString("N"),
            SessionId = sessionId ?? DefaultSessionId
        });
        return message;
    }

    private static SessionCompaction CreateCompaction(
        string? id = null,
        string? sessionId = null,
        string summary = "test summary",
        int level = 1)
    {
        return new SessionCompaction
        {
            Id = id ?? Guid.NewGuid().ToString("N"),
            SessionId = sessionId ?? DefaultSessionId,
            CompactedSummary = summary,
            SourceMessageIds = ["msg-1", "msg-2"],
            OriginalTokenCount = 100,
            CompactedTokenCount = 20,
            CompactionLevel = level,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static ChatSession CreateSession(
        string? sessionId = null,
        int messageCount = 0,
        int tokenCount = 0)
    {
        return new ChatSession
        {
            SessionId = sessionId ?? DefaultSessionId,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow,
            TotalMessageCount = messageCount,
            TotalTokenCount = tokenCount
        };
    }

    #endregion

    #region GetMessagesAsync

    [Fact]
    public async Task GetMessagesAsync_NoMessages_ReturnsEmpty()
    {
        var sut = CreateSut();

        var result = await sut.GetMessagesAsync(DefaultSessionId, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetMessagesAsync_ReturnsMessagesOrderedBySequence()
    {
        var sqlClient = CreateSqliteClient();
        var sut = CreateSut(sqlClient);

        await sut.SaveMessagesAsync(DefaultSessionId,
        [
            CreateMessage("First"),
            CreateMessage("Second"),
            CreateMessage("Third")
        ], TestContext.Current.CancellationToken);

        var result = await sut.GetMessagesAsync(DefaultSessionId, TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Count);
        Assert.Contains("First", result[0].Text);
        Assert.Contains("Second", result[1].Text);
        Assert.Contains("Third", result[2].Text);
    }

    [Fact]
    public async Task GetMessagesAsync_IsolatesBySession()
    {
        var sqlClient = CreateSqliteClient();
        var sut = CreateSut(sqlClient);

        await sut.SaveMessagesAsync(DefaultSessionId, [CreateMessage("Session A msg")], TestContext.Current.CancellationToken);
        await sut.SaveMessagesAsync(OtherSessionId, [CreateMessage("Session B msg", sessionId: OtherSessionId)], TestContext.Current.CancellationToken);

        var resultA = await sut.GetMessagesAsync(DefaultSessionId, TestContext.Current.CancellationToken);
        var resultB = await sut.GetMessagesAsync(OtherSessionId, TestContext.Current.CancellationToken);

        Assert.Single(resultA);
        Assert.Contains("Session A msg", resultA[0].Text);
        Assert.Single(resultB);
        Assert.Contains("Session B msg", resultB[0].Text);
    }

    #endregion

    #region GetMessageByIdAsync

    [Fact]
    public async Task GetMessageByIdAsync_MessageExists_ReturnsMessage()
    {
        var sqlClient = CreateSqliteClient();
        var sut = CreateSut(sqlClient);
        var messageId = "msg-id-1";

        await sut.SaveMessagesAsync(DefaultSessionId,
            [CreateMessage("Hello world", messageId: messageId)],
            TestContext.Current.CancellationToken);

        var result = await sut.GetMessageByIdAsync(messageId, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Contains("Hello world", result.Text);

        // Verify metadata is attached
        var meta = result.Contents.OfType<MemoryMetadataContent>().FirstOrDefault();
        Assert.NotNull(meta);
        Assert.Equal(messageId, meta.MessageId);
    }

    [Fact]
    public async Task GetMessageByIdAsync_MessageDoesNotExist_ReturnsNull()
    {
        var sut = CreateSut();

        var result = await sut.GetMessageByIdAsync("nonexistent", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    #endregion

    #region SaveMessagesAsync

    [Fact]
    public async Task SaveMessagesAsync_EmptyList_DoesNothing()
    {
        var sut = CreateSut();

        await sut.SaveMessagesAsync(DefaultSessionId, [], TestContext.Current.CancellationToken);

        var result = await sut.GetMessagesAsync(DefaultSessionId, TestContext.Current.CancellationToken);
        Assert.Empty(result);
    }

    [Fact]
    public async Task SaveMessagesAsync_SingleMessage_PersistsCorrectly()
    {
        var sqlClient = CreateSqliteClient();
        var sut = CreateSut(sqlClient);

        await sut.SaveMessagesAsync(DefaultSessionId,
            [CreateMessage("Test message")],
            TestContext.Current.CancellationToken);

        var result = await sut.GetMessagesAsync(DefaultSessionId, TestContext.Current.CancellationToken);
        Assert.Single(result);
        Assert.Equal(ChatRole.User, result[0].Role);
        Assert.Contains("Test message", result[0].Text);
    }

    [Fact]
    public async Task SaveMessagesAsync_Upsert_UpdatesExistingMessage()
    {
        var sqlClient = CreateSqliteClient();
        var sut = CreateSut(sqlClient);
        var messageId = "upsert-msg";

        // First insert
        await sut.SaveMessagesAsync(DefaultSessionId,
            [CreateMessage("Original text", messageId: messageId)],
            TestContext.Current.CancellationToken);

        // Second insert with same ID (simulating update)
        var updatedMessage = CreateMessage("Updated text", messageId: messageId);
        await sut.SaveMessagesAsync(DefaultSessionId, [updatedMessage], TestContext.Current.CancellationToken);

        var result = await sut.GetMessageByIdAsync(messageId, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Contains("Updated text", result.Text);
    }

    [Fact]
    public async Task SaveMessagesAsync_MultipleMessages_AssignsCorrectSequence()
    {
        var sqlClient = CreateSqliteClient();
        var sut = CreateSut(sqlClient);

        await sut.SaveMessagesAsync(DefaultSessionId,
        [
            CreateMessage("Msg 0"),
            CreateMessage("Msg 1"),
            CreateMessage("Msg 2")
        ], TestContext.Current.CancellationToken);

        // Directly query entities to verify sequence numbers
        var entities = await sqlClient.Queryable<ChatMessageEntity>()
            .Where(e => e.SessionId == DefaultSessionId)
            .OrderBy(e => e.SequenceNumber)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, entities.Count);
        Assert.Equal(0, entities[0].SequenceNumber);
        Assert.Equal(1, entities[1].SequenceNumber);
        Assert.Equal(2, entities[2].SequenceNumber);
    }

    #endregion

    #region GetCompactionsAsync

    [Fact]
    public async Task GetCompactionsAsync_NoCompactions_ReturnsEmpty()
    {
        var sut = CreateSut();

        var result = await sut.GetCompactionsAsync(DefaultSessionId, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCompactionsAsync_ReturnsCompactionsOrderedByLevel()
    {
        var sqlClient = CreateSqliteClient();
        var sut = CreateSut(sqlClient);

        await sut.SaveCompactionAsync(CreateCompaction(level: 3, summary: "Level 3"), TestContext.Current.CancellationToken);
        await sut.SaveCompactionAsync(CreateCompaction(level: 1, summary: "Level 1"), TestContext.Current.CancellationToken);
        await sut.SaveCompactionAsync(CreateCompaction(level: 2, summary: "Level 2"), TestContext.Current.CancellationToken);

        var result = await sut.GetCompactionsAsync(DefaultSessionId, TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Count);
        Assert.Equal(1, result[0].CompactionLevel);
        Assert.Equal(2, result[1].CompactionLevel);
        Assert.Equal(3, result[2].CompactionLevel);
    }

    [Fact]
    public async Task GetCompactionsAsync_IsolatesBySession()
    {
        var sqlClient = CreateSqliteClient();
        var sut = CreateSut(sqlClient);

        await sut.SaveCompactionAsync(CreateCompaction(sessionId: DefaultSessionId, summary: "A"), TestContext.Current.CancellationToken);
        await sut.SaveCompactionAsync(CreateCompaction(sessionId: OtherSessionId, summary: "B"), TestContext.Current.CancellationToken);

        var resultA = await sut.GetCompactionsAsync(DefaultSessionId, TestContext.Current.CancellationToken);
        var resultB = await sut.GetCompactionsAsync(OtherSessionId, TestContext.Current.CancellationToken);

        Assert.Single(resultA);
        Assert.Equal("A", resultA[0].CompactedSummary);
        Assert.Single(resultB);
        Assert.Equal("B", resultB[0].CompactedSummary);
    }

    #endregion

    #region SaveCompactionAsync

    [Fact]
    public async Task SaveCompactionAsync_PersistsCorrectly()
    {
        var sqlClient = CreateSqliteClient();
        var sut = CreateSut(sqlClient);
        var compactionId = "compaction-xyz";

        var compaction = new SessionCompaction
        {
            Id = compactionId,
            SessionId = DefaultSessionId,
            CompactedSummary = "This is a test summary",
            SourceMessageIds = ["msg-a", "msg-b", "msg-c"],
            OriginalTokenCount = 300,
            CompactedTokenCount = 50,
            CompactionLevel = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await sut.SaveCompactionAsync(compaction, TestContext.Current.CancellationToken);

        var result = await sut.GetCompactionsAsync(DefaultSessionId, TestContext.Current.CancellationToken);
        Assert.Single(result);
        Assert.Equal(compactionId, result[0].Id);
        Assert.Equal("This is a test summary", result[0].CompactedSummary);
        Assert.Equal(3, result[0].SourceMessageIds.Count);
        Assert.Equal(300, result[0].OriginalTokenCount);
        Assert.Equal(50, result[0].CompactedTokenCount);
    }

    #endregion

    #region GetSessionAsync

    [Fact]
    public async Task GetSessionAsync_SessionExists_ReturnsSession()
    {
        var sqlClient = CreateSqliteClient();
        var sut = CreateSut(sqlClient);

        await sut.SaveSessionAsync(CreateSession(sessionId: DefaultSessionId, messageCount: 42, tokenCount: 1000),
            TestContext.Current.CancellationToken);

        var result = await sut.GetSessionAsync(DefaultSessionId, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(DefaultSessionId, result.SessionId);
        Assert.Equal(42, result.TotalMessageCount);
        Assert.Equal(1000, result.TotalTokenCount);
    }

    [Fact]
    public async Task GetSessionAsync_SessionDoesNotExist_ReturnsNull()
    {
        var sut = CreateSut();

        var result = await sut.GetSessionAsync("nonexistent-session", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    #endregion

    #region SaveSessionAsync

    [Fact]
    public async Task SaveSessionAsync_NewSession_PersistsCorrectly()
    {
        var sqlClient = CreateSqliteClient();
        var sut = CreateSut(sqlClient);

        var session = CreateSession(sessionId: DefaultSessionId, messageCount: 5, tokenCount: 500);

        await sut.SaveSessionAsync(session, TestContext.Current.CancellationToken);

        var result = await sut.GetSessionAsync(DefaultSessionId, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(5, result.TotalMessageCount);
        Assert.Equal(500, result.TotalTokenCount);
    }

    [Fact]
    public async Task SaveSessionAsync_Upsert_UpdatesExistingSession()
    {
        var sqlClient = CreateSqliteClient();
        var sut = CreateSut(sqlClient);

        // First save
        await sut.SaveSessionAsync(CreateSession(sessionId: DefaultSessionId, messageCount: 10),
            TestContext.Current.CancellationToken);

        // Update
        await sut.SaveSessionAsync(CreateSession(sessionId: DefaultSessionId, messageCount: 20, tokenCount: 2000),
            TestContext.Current.CancellationToken);

        var result = await sut.GetSessionAsync(DefaultSessionId, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(20, result.TotalMessageCount);
        Assert.Equal(2000, result.TotalTokenCount);
    }

    #endregion
}
