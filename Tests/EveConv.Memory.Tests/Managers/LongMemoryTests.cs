using EveConv.Abstraction.Memory;
using EveConv.Memory.Config;
using EveConv.Memory.Managers;
using EveConv.Memory.Models;
using EveConv.Memory.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SqlSugar;

namespace EveConv.Memory.Tests.Managers;

/// <summary>
/// Unit tests for <see cref="LongMemory"/> using SQLite in-memory database.
/// Each test gets a fresh database instance — no shared state, no cleanup needed.
/// </summary>
public class LongMemoryTests
{
    private const string DefaultOwner = "test-owner";
    private const string OtherOwner = "other-owner";

    #region helpers

    /// <summary>
    /// Creates an in-memory SQLite <see cref="ISqlSugarClient"/> with the
    /// <c>long_memory_entries</c> table pre-created.
    /// </summary>
    private static SqlSugarScope CreateSqliteClient()
    {
        var client = new SqlSugarScope(new ConnectionConfig
        {
            DbType = DbType.Sqlite,
            ConnectionString = "DataSource=:memory:",
            IsAutoCloseConnection = false // keep connection alive so all ops share the same in-memory db
        });

        client.DbMaintenance.CreateDatabase();
        client.CodeFirst.InitTables<LongMemoryEntryEntity>();

        return client;
    }

    /// <summary>Creates a <see cref="LongMemory"/> instance wired to a fresh SQLite database.</summary>
    private static LongMemory CreateSut(
        ISqlSugarClient? sqlClient = null,
        MemoryConfiguration? config = null,
        ITokenCounter? tokenCounter = null)
    {
        sqlClient ??= CreateSqliteClient();
        config ??= new MemoryConfiguration();
        tokenCounter ??= new TiktokenCounter();

        // LLMLongMemoryExtractor is sealed — create a real instance with a mocked IChatClient.
        // The extractor is never invoked in these tests (we call UpsertAsync directly).
        var extractor = new LLMLongMemoryExtractor(
            Mock.Of<IChatClient>(),
            Options.Create(config),
            NullLoggerFactory.Instance);

        var sessionMock = new Mock<ISessionMemory>();

        return new LongMemory(
            extractor,
            sessionMock.Object,
            tokenCounter,
            Options.Create(config),
            sqlClient,
            NullLoggerFactory.Instance);
    }

    /// <summary>Creates a <see cref="LongMemoryEntry"/> with sane defaults.</summary>
    private static LongMemoryEntry CreateEntry(
        string? id = null,
        string? ownerKey = null,
        string category = "fact",
        string content = "test content",
        float importance = 0.8f,
        string[]? sourceSessionIds = null)
    {
        return new LongMemoryEntry
        {
            Id = id ?? Guid.NewGuid().ToString("N"),
            OwnerKey = ownerKey ?? DefaultOwner,
            Category = category,
            Content = content,
            Importance = importance,
            SourceSessionIds = sourceSessionIds ?? ["session-1"],
            CreatedAt = DateTimeOffset.UtcNow,
            LastReinforcedAt = DateTimeOffset.UtcNow
        };
    }

    #endregion

    #region GetContextMessagesAsync

    [Fact]
    public async Task GetContextMessagesAsync_NoEntries_ReturnsEmpty()
    {
        var sut = CreateSut();

        var result = await sut.GetContextMessagesAsync(DefaultOwner, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetContextMessagesAsync_ReturnsOneMessagePerCategory()
    {
        var sqlClient = CreateSqliteClient();
        var sut = CreateSut(sqlClient);

        // Seed: two facts, one preference
        await sut.UpsertAsync(DefaultOwner, CreateEntry(category: "fact", content: "Fact A", importance: 0.5f), TestContext.Current.CancellationToken);
        await sut.UpsertAsync(DefaultOwner, CreateEntry(category: "fact", content: "Fact B", importance: 0.9f), TestContext.Current.CancellationToken);
        await sut.UpsertAsync(DefaultOwner, CreateEntry(category: "preference", content: "Likes dark mode", importance: 0.7f), TestContext.Current.CancellationToken);

        var result = await sut.GetContextMessagesAsync(DefaultOwner, TestContext.Current.CancellationToken);

        // One per category = 2 messages
        Assert.Equal(2, result.Count);

        // Each message is a System message
        Assert.All(result, m => Assert.Equal(ChatRole.System, m.Role));

        // Fact category should contain the higher-importance entry
        var factMsg = result.Single(m => m.Text.Contains("(fact)"));
        Assert.Contains("Fact B", factMsg.Text);

        var prefMsg = result.Single(m => m.Text.Contains("(preference)"));
        Assert.Contains("Likes dark mode", prefMsg.Text);
    }

    [Fact]
    public async Task GetContextMessagesAsync_RespectsTokenBudget()
    {
        var sqlClient = CreateSqliteClient();
        var config = new MemoryConfiguration { LongMemoryMaxTokens = 5 }; // very tight budget
        var sut = CreateSut(sqlClient, config);

        // Seed entries across categories — each formatted as "(category) content"
        await sut.UpsertAsync(DefaultOwner, CreateEntry(category: "fact", content: "A long fact about the user", importance: 0.9f), TestContext.Current.CancellationToken);
        await sut.UpsertAsync(DefaultOwner, CreateEntry(category: "preference", content: "Prefers short responses", importance: 0.8f), TestContext.Current.CancellationToken);

        var result = await sut.GetContextMessagesAsync(DefaultOwner, TestContext.Current.CancellationToken);

        // With 5 token budget, at most 1 entry fits (each entry is ~4+ chars → ~2 tokens)
        Assert.True(result.Count <= 1);
    }

    [Fact]
    public async Task GetContextMessagesAsync_IsolatesByOwner()
    {
        var sqlClient = CreateSqliteClient();
        var sut = CreateSut(sqlClient);

        await sut.UpsertAsync(DefaultOwner, CreateEntry(content: "Owner A fact", importance: 0.9f), TestContext.Current.CancellationToken);
        await sut.UpsertAsync(OtherOwner, CreateEntry(ownerKey: OtherOwner, content: "Owner B fact", importance: 0.9f), TestContext.Current.CancellationToken);

        var resultA = await sut.GetContextMessagesAsync(DefaultOwner, TestContext.Current.CancellationToken);
        var resultB = await sut.GetContextMessagesAsync(OtherOwner, TestContext.Current.CancellationToken);

        Assert.Single(resultA);
        Assert.Contains("Owner A fact", resultA[0].Text);
        Assert.Single(resultB);
        Assert.Contains("Owner B fact", resultB[0].Text);
    }

    [Fact]
    public async Task GetContextMessagesAsync_CategoriesOrderedAlphabetically()
    {
        var sqlClient = CreateSqliteClient();
        var sut = CreateSut(sqlClient);

        await sut.UpsertAsync(DefaultOwner, CreateEntry(category: "event", content: "E", importance: 0.9f), TestContext.Current.CancellationToken);
        await sut.UpsertAsync(DefaultOwner, CreateEntry(category: "fact", content: "F", importance: 0.9f), TestContext.Current.CancellationToken);
        await sut.UpsertAsync(DefaultOwner, CreateEntry(category: "habit", content: "H", importance: 0.9f), TestContext.Current.CancellationToken);
        await sut.UpsertAsync(DefaultOwner, CreateEntry(category: "preference", content: "P", importance: 0.9f), TestContext.Current.CancellationToken);

        var result = await sut.GetContextMessagesAsync(DefaultOwner, TestContext.Current.CancellationToken);

        Assert.Equal(4, result.Count);
        // Categories ordered alphabetically: event, fact, habit, preference
        Assert.Contains("(event)", result[0].Text);
        Assert.Contains("(fact)", result[1].Text);
        Assert.Contains("(habit)", result[2].Text);
        Assert.Contains("(preference)", result[3].Text);
    }

    #endregion

    #region UpsertAsync

    [Fact]
    public async Task UpsertAsync_NewEntry_InsertsSuccessfully()
    {
        var sqlClient = CreateSqliteClient();
        var sut = CreateSut(sqlClient);
        var entryId = Guid.NewGuid().ToString("N");

        await sut.UpsertAsync(DefaultOwner, CreateEntry(
            id: entryId, category: "fact", content: "User is a developer", importance: 0.8f,
            sourceSessionIds: ["s1", "s2"]), TestContext.Current.CancellationToken);

        // Verify via GetContextMessagesAsync
        var messages = await sut.GetContextMessagesAsync(DefaultOwner, TestContext.Current.CancellationToken);
        Assert.Single(messages);
        Assert.Contains("User is a developer", messages[0].Text);
    }

    [Fact]
    public async Task UpsertAsync_DuplicateContent_UpdatesExisting()
    {
        var sqlClient = CreateSqliteClient();
        var sut = CreateSut(sqlClient);

        // First upsert
        await sut.UpsertAsync(DefaultOwner, CreateEntry(
            id: "entry-1", content: "I love coffee", importance: 0.5f,
            sourceSessionIds: ["s1"]), TestContext.Current.CancellationToken);

        // Second upsert with same content but different ID and higher importance
        await sut.UpsertAsync(DefaultOwner, CreateEntry(
            id: "entry-2", content: "I love coffee", importance: 0.9f,
            sourceSessionIds: ["s2"]), TestContext.Current.CancellationToken);

        // Should still be only one entry (dedup by content)
        var messages = await sut.GetContextMessagesAsync(DefaultOwner, TestContext.Current.CancellationToken);
        Assert.Single(messages);

        // Verify the row in DB: importance should be max(0.5, 0.9) = 0.9
        var entities = await sqlClient.Queryable<LongMemoryEntryEntity>()
            .Where(e => e.OwnerKey == DefaultOwner)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(entities);
        Assert.Equal(0.9f, entities[0].Importance);
        // Original ID should be preserved (first insert wins)
        Assert.Equal("entry-1", entities[0].Id);
    }

    [Fact]
    public async Task UpsertAsync_DuplicateContent_MergesSessionIds()
    {
        var sqlClient = CreateSqliteClient();
        var sut = CreateSut(sqlClient);

        await sut.UpsertAsync(DefaultOwner, CreateEntry(
            id: "entry-1", content: "Prefers Python", importance: 0.7f,
            sourceSessionIds: ["s1", "s2"]), TestContext.Current.CancellationToken);

        await sut.UpsertAsync(DefaultOwner, CreateEntry(
            id: "entry-2", content: "Prefers Python", importance: 0.8f,
            sourceSessionIds: ["s2", "s3"]), TestContext.Current.CancellationToken);

        var entities = await sqlClient.Queryable<LongMemoryEntryEntity>()
            .Where(e => e.OwnerKey == DefaultOwner)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(entities);

        // Source sessions should be merged: s1, s2, s3
        var sourceJson = entities[0].SourceSessionIdsJson;
        Assert.Contains("s1", sourceJson);
        Assert.Contains("s2", sourceJson);
        Assert.Contains("s3", sourceJson);
    }

    [Fact]
    public async Task UpsertAsync_DifferentContent_InsertsSeparately()
    {
        var sqlClient = CreateSqliteClient();
        var sut = CreateSut(sqlClient);

        await sut.UpsertAsync(DefaultOwner, CreateEntry(content: "Fact one", category: "fact"), TestContext.Current.CancellationToken);
        await sut.UpsertAsync(DefaultOwner, CreateEntry(content: "Fact two", category: "fact"), TestContext.Current.CancellationToken);

        // Two separate entries in same category
        var messages = await sut.GetContextMessagesAsync(DefaultOwner, TestContext.Current.CancellationToken);
        // Both are "fact" category — only highest importance is returned
        Assert.Single(messages);

        // But DB has both
        var entities = await sqlClient.Queryable<LongMemoryEntryEntity>()
            .Where(e => e.OwnerKey == DefaultOwner)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, entities.Count);
    }

    [Fact]
    public async Task UpsertAsync_IsolatesByOwner()
    {
        var sqlClient = CreateSqliteClient();
        var sut = CreateSut(sqlClient);

        await sut.UpsertAsync(DefaultOwner, CreateEntry(content: "Shared content", importance: 0.5f), TestContext.Current.CancellationToken);
        await sut.UpsertAsync(OtherOwner, CreateEntry(ownerKey: OtherOwner, content: "Shared content", importance: 0.9f), TestContext.Current.CancellationToken);

        // Each owner has their own entry (same content, different owners)
        var entriesA = await sqlClient.Queryable<LongMemoryEntryEntity>()
            .Where(e => e.OwnerKey == DefaultOwner).ToListAsync(TestContext.Current.CancellationToken);
        var entriesB = await sqlClient.Queryable<LongMemoryEntryEntity>()
            .Where(e => e.OwnerKey == OtherOwner).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(entriesA);
        Assert.Single(entriesB);
        Assert.Equal(0.5f, entriesA[0].Importance);
        Assert.Equal(0.9f, entriesB[0].Importance);
    }

    #endregion

    #region ForgetAsync

    [Fact]
    public async Task ForgetAsync_ExistingEntry_DeletesSuccessfully()
    {
        var sqlClient = CreateSqliteClient();
        var sut = CreateSut(sqlClient);
        var entryId = "entry-to-forget";

        await sut.UpsertAsync(DefaultOwner, CreateEntry(id: entryId, content: "Temporary fact"), TestContext.Current.CancellationToken);
        await sut.ForgetAsync(DefaultOwner, entryId, TestContext.Current.CancellationToken);

        var messages = await sut.GetContextMessagesAsync(DefaultOwner, TestContext.Current.CancellationToken);
        Assert.Empty(messages);
    }

    [Fact]
    public async Task ForgetAsync_NonExistentEntry_DoesNotThrow()
    {
        var sut = CreateSut();

        // Should not throw
        await sut.ForgetAsync(DefaultOwner, "nonexistent-id", TestContext.Current.CancellationToken);

        var messages = await sut.GetContextMessagesAsync(DefaultOwner, TestContext.Current.CancellationToken);
        Assert.Empty(messages);
    }

    [Fact]
    public async Task ForgetAsync_IsolatesByOwner()
    {
        var sqlClient = CreateSqliteClient();
        var sut = CreateSut(sqlClient);

        await sut.UpsertAsync(DefaultOwner, CreateEntry(id: "entry-a", content: "Owner A fact"), TestContext.Current.CancellationToken);
        await sut.UpsertAsync(OtherOwner, CreateEntry(ownerKey: OtherOwner, id: "entry-b", content: "Owner B fact"), TestContext.Current.CancellationToken);

        // Forget Owner A's entry
        await sut.ForgetAsync(DefaultOwner, "entry-a", TestContext.Current.CancellationToken);

        // Owner A should have no entries, Owner B still has theirs
        Assert.Empty(await sut.GetContextMessagesAsync(DefaultOwner, TestContext.Current.CancellationToken));
        Assert.Single(await sut.GetContextMessagesAsync(OtherOwner, TestContext.Current.CancellationToken));
    }

    #endregion
}
