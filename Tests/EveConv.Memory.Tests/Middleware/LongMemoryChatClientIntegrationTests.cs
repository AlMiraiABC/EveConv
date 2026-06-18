using System.ClientModel;
using dotenv.net;
using EveConv.Abstraction.Memory;
using EveConv.Memory.Config;
using EveConv.Memory.Managers;
using EveConv.Memory.Middleware;
using EveConv.Memory.Models;
using EveConv.Memory.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenAI;
using SqlSugar;

namespace EveConv.Memory.Tests.Middleware;

/// <summary>
/// Integration tests for <see cref="LongMemoryChatClient"/> using a real
/// OpenAI-compatible API, <see cref="LongMemory"/>, <see cref="SessionMemory"/>,
/// and <see cref="TiktokenCounter"/> backed by SQLite in-memory database.
/// Reads <c>OPENAI_ENDPOINT</c>, <c>OPENAI_MODEL_ID</c>, and <c>OPENAI_API_KEY</c>
/// from the <c>.env</c> file.
/// </summary>
public class LongMemoryChatClientIntegrationTests
{
    private const string DefaultSessionId = "integration-session-lm-001";
    private const string DefaultOwnerKey = "integration-owner";

    private readonly string _endpoint;
    private readonly string _modelId;
    private readonly string _apiKey;

    public LongMemoryChatClientIntegrationTests()
    {
        DotEnv.Load();

        _endpoint = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT") ?? throw new ArgumentNullException();
        _modelId = Environment.GetEnvironmentVariable("OPENAI_MODEL_ID") ?? throw new ArgumentNullException();
        _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new ArgumentNullException();
    }

    #region helpers

    /// <summary>Create a real OpenAI-compatible <see cref="IChatClient"/>.</summary>
    private IChatClient CreateChatClient()
    {
        var client = new OpenAIClient(
            new ApiKeyCredential(_apiKey),
            new OpenAIClientOptions { Endpoint = new Uri(_endpoint) });
        return client.GetChatClient(_modelId).AsIChatClient();
    }

    /// <summary>
    /// Creates an in-memory SQLite <see cref="ISqlSugarClient"/> with all
    /// required tables pre-created for both <see cref="LongMemory"/> and
    /// <see cref="SessionMemory"/>.
    /// </summary>
    private static SqlSugarScope CreateSqliteClient()
    {
        var client = new SqlSugarScope(new ConnectionConfig
        {
            DbType = DbType.Sqlite,
            ConnectionString = "DataSource=:memory:",
            IsAutoCloseConnection = false // keep alive so all ops share same in-memory db
        });

        client.DbMaintenance.CreateDatabase();
        client.CodeFirst.InitTables<LongMemoryEntryEntity>();
        client.CodeFirst.InitTables<ChatMessageEntity>();
        client.CodeFirst.InitTables<SessionCompactionEntity>();
        client.CodeFirst.InitTables<ChatSessionEntity>();

        return client;
    }

    /// <summary>
    /// Creates a <see cref="LongMemory"/> wired to a fresh SQLite database,
    /// real <see cref="TiktokenCounter"/>, <see cref="SessionMemory"/>,
    /// and a real OpenAI chat client for extraction.
    /// </summary>
    private static LongMemory CreateLongMemory(
        SqlSugarScope sqlClient,
        IChatClient extractionClient,
        MemoryConfiguration? config = null)
    {
        config ??= new MemoryConfiguration
        {
            LongMemoryMaxTokens = 500,
            ImportanceThreshold = 0.3f
        };

        var tokenCounter = new TiktokenCounter(NullLoggerFactory.Instance);
        var session = new SessionMemory(sqlClient, NullLoggerFactory.Instance);
        var extractor = new LLMLongMemoryExtractor(
            extractionClient,
            Options.Create(config),
            NullLoggerFactory.Instance);

        return new LongMemory(
            extractor,
            session,
            tokenCounter,
            Options.Create(config),
            sqlClient,
            NullLoggerFactory.Instance);
    }

    /// <summary>
    /// Creates the full <see cref="LongMemoryChatClient"/> pipeline.
    /// </summary>
    private static LongMemoryChatClient CreateSut(
        IChatClient chatClient,
        LongMemory longMemory,
        string? ownerKey = null)
    {
        var options = Options.Create(new LongMemoryOptions
        {
            OwnerKey = ownerKey ?? DefaultOwnerKey
        });

        return new LongMemoryChatClient(
            chatClient,
            longMemory,
            options,
            NullLoggerFactory.Instance);
    }

    /// <summary>
    /// Creates a <see cref="ChatMessage"/> with <see cref="MemoryMetadataContent"/> attached.
    /// </summary>
    private static ChatMessage CreateMsg(
        string role,
        string text,
        string messageId,
        string? sessionId = null)
    {
        var msg = new ChatMessage(new ChatRole(role), text);
        msg.Contents.Add(new MemoryMetadataContent
        {
            MessageId = messageId,
            SessionId = sessionId ?? DefaultSessionId
        });
        return msg;
    }

    /// <summary>
    /// Seeds a single long-term memory entry via direct upsert (no LLM extraction).
    /// </summary>
    private static async Task SeedLongMemoryAsync(
        LongMemory longMemory,
        string ownerKey,
        string category,
        string content,
        float importance = 0.9f,
        CancellationToken ct = default)
    {
        await longMemory.UpsertAsync(ownerKey, new LongMemoryEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            OwnerKey = ownerKey,
            Category = category,
            Content = content,
            Importance = importance,
            SourceSessionIds = [DefaultSessionId],
            CreatedAt = DateTimeOffset.UtcNow,
            LastReinforcedAt = DateTimeOffset.UtcNow
        }, ct);
    }

    #endregion

    #region GetResponseAsync — injection with real LLM and real LongMemory

    /// <summary>
    /// Integration test: long-term memory entries are injected before the
    /// user message and the real LLM generates a response that references
    /// the injected context.
    /// </summary>
    [Fact]
    public async Task GetResponseAsync_WithLongMemory_LLMReferencesInjectedContext()
    {
        // Arrange
        var sqlClient = CreateSqliteClient();
        var chatClient = CreateChatClient();
        var longMemory = CreateLongMemory(sqlClient, chatClient);

        // Seed long memory entries
        await SeedLongMemoryAsync(longMemory, DefaultOwnerKey,
            "preference", "Alice is a backend engineer at Contoso who loves C# and .NET.",
            ct: TestContext.Current.CancellationToken);
        await SeedLongMemoryAsync(longMemory, DefaultOwnerKey,
            "habit", "Alice goes hiking every Saturday morning, rain or shine.",
            ct: TestContext.Current.CancellationToken);

        var sut = CreateSut(chatClient, longMemory);

        var requestMsg = CreateMsg("user", "What is my name and what do I do for work?", "msg-001");

        // Act
        var response = await sut.GetResponseAsync([requestMsg], ct: TestContext.Current.CancellationToken);

        // Assert — got a real response
        Assert.NotEmpty(response.Messages);
        var assistantMsg = response.Messages.FirstOrDefault(m => m.Role == ChatRole.Assistant);
        Assert.NotNull(assistantMsg);
        Assert.False(string.IsNullOrWhiteSpace(assistantMsg.Text));

        // The LLM should reference the injected long memory context
        var text = assistantMsg.Text;
        Assert.True(
            text.Contains("Alice", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Contoso", StringComparison.OrdinalIgnoreCase)
            || text.Contains("engineer", StringComparison.OrdinalIgnoreCase),
            $"Expected LLM to reference long memory context. Got: {text}");
    }

    /// <summary>
    /// Integration test: without long-term memory entries, the real LLM
    /// still responds normally. The pipeline handles empty long memory gracefully.
    /// </summary>
    [Fact]
    public async Task GetResponseAsync_NoLongMemory_StillGetsRealResponse()
    {
        // Arrange
        var sqlClient = CreateSqliteClient();
        var chatClient = CreateChatClient();
        var longMemory = CreateLongMemory(sqlClient, chatClient);

        // No seed — long memory is empty

        var sut = CreateSut(chatClient, longMemory);

        var requestMsg = CreateMsg("user", "Say hello in exactly two words.", "msg-001");

        // Act
        var response = await sut.GetResponseAsync([requestMsg], ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(response.Messages);
        var assistantMsg = response.Messages.FirstOrDefault(m => m.Role == ChatRole.Assistant);
        Assert.NotNull(assistantMsg);
        Assert.False(string.IsNullOrWhiteSpace(assistantMsg.Text));
    }

    /// <summary>
    /// Integration test: multi-turn conversation with long-term memory
    /// entries injected on every turn. Verifies the LLM can reference the
    /// persistent context across turns.
    /// </summary>
    [Fact]
    public async Task GetResponseAsync_MultiTurn_ContextPersistsAcrossTurns()
    {
        // Arrange
        var sqlClient = CreateSqliteClient();
        var chatClient = CreateChatClient();
        var longMemory = CreateLongMemory(sqlClient, chatClient);

        // Seed long memory about the dog
        await SeedLongMemoryAsync(longMemory, DefaultOwnerKey,
            "preference", "Alice has a golden retriever puppy named Max who is 3 months old.",
            ct: TestContext.Current.CancellationToken);

        var sut = CreateSut(chatClient, longMemory);

        // Turn 1
        var turn1Msg = CreateMsg("user", "What is my dog's name?", "msg-001");
        var response1 = await sut.GetResponseAsync([turn1Msg], ct: TestContext.Current.CancellationToken);
        Assert.NotEmpty(response1.Messages);

        // Turn 2 — include turn 1 history
        var turn2Msg = CreateMsg("user", "How old is he?", "msg-002");
        var turn2Messages = new List<ChatMessage>
        {
            turn1Msg,
            response1.Messages.First(m => m.Role == ChatRole.Assistant),
            turn2Msg
        };
        var response2 = await sut.GetResponseAsync(turn2Messages, ct: TestContext.Current.CancellationToken);
        Assert.NotEmpty(response2.Messages);

        // Assert — LLM should reference the long memory context
        var turn2Text = response2.Messages
            .First(m => m.Role == ChatRole.Assistant).Text;
        Assert.True(
            turn2Text.Contains("3", StringComparison.OrdinalIgnoreCase)
            || turn2Text.Contains("month", StringComparison.OrdinalIgnoreCase)
            || turn2Text.Contains("puppy", StringComparison.OrdinalIgnoreCase),
            $"Expected LLM to know the dog's age from context. Got: {turn2Text}");
    }

    /// <summary>
    /// Integration test: long memory isolation — entries for different
    /// owner keys do not leak across owners.
    /// </summary>
    [Fact]
    public async Task GetResponseAsync_DifferentOwner_NoContextLeakage()
    {
        // Arrange
        var sqlClient = CreateSqliteClient();
        var chatClient = CreateChatClient();
        var longMemory = CreateLongMemory(sqlClient, chatClient);

        const string otherOwner = "other-owner";

        // Seed data for OTHER owner — should NOT be visible to DefaultOwnerKey
        await SeedLongMemoryAsync(longMemory, otherOwner,
            "preference", "Bob loves Python and machine learning.", ct: TestContext.Current.CancellationToken);

        var sut = CreateSut(chatClient, longMemory);

        var requestMsg = CreateMsg("user",
            "Do you know anything about me? Answer in one short sentence.", "msg-001");

        // Act
        var response = await sut.GetResponseAsync([requestMsg], ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(response.Messages);
        var assistantMsg = response.Messages.FirstOrDefault(m => m.Role == ChatRole.Assistant);
        Assert.NotNull(assistantMsg);

        // The LLM should NOT reference Bob's data since it belongs to a different owner
        Assert.DoesNotContain("Bob", assistantMsg.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Python", assistantMsg.Text, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region GetStreamingResponseAsync — injection with real LLM

    /// <summary>
    /// Integration test: streaming response from a real LLM with long-term
    /// memory entries injected before the request.
    /// </summary>
    [Fact]
    public async Task GetStreamingResponseAsync_WithLongMemory_StreamsRealResponse()
    {
        // Arrange
        var sqlClient = CreateSqliteClient();
        var chatClient = CreateChatClient();
        var longMemory = CreateLongMemory(sqlClient, chatClient);

        // Seed long memory about color preference
        await SeedLongMemoryAsync(longMemory, DefaultOwnerKey,
            "preference", "Alice's favorite color is teal and she loves minimalist design.",
            ct: TestContext.Current.CancellationToken);

        var sut = CreateSut(chatClient, longMemory);

        var requestMsg = CreateMsg("user",
            "Based on what you know about me, what color would you recommend for a new logo? " +
            "Answer in one short sentence.", "msg-001");

        // Act
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in sut.GetStreamingResponseAsync(
                           [requestMsg], ct: TestContext.Current.CancellationToken))
        {
            updates.Add(update);
        }

        // Assert — streaming produced output
        Assert.NotEmpty(updates);
        var fullText = string.Concat(updates.Where(u => u.Text is not null).Select(u => u.Text));
        Assert.False(string.IsNullOrWhiteSpace(fullText));

        // The response should reference the color preference
        Assert.True(
            fullText.Contains("teal", StringComparison.OrdinalIgnoreCase)
            || fullText.Contains("blue", StringComparison.OrdinalIgnoreCase)
            || fullText.Contains("color", StringComparison.OrdinalIgnoreCase),
            $"Expected streaming response to reference the color preference. Got: {fullText}");
    }

    #endregion

    #region Background extraction — real LLM

    /// <summary>
    /// Integration test: directly calls <see cref="LongMemory.ExtractAndStoreAsync"/>
    /// with pre-seeded session messages and verifies the real LLM extractor produces
    /// long-term memory entries that are persisted to SQLite.
    /// </summary>
    [Fact]
    public async Task ExtractAndStoreAsync_WithSessionMessages_ExtractsAndStoresEntries()
    {
        // Arrange
        var sqlClient = CreateSqliteClient();
        var chatClient = CreateChatClient();
        var longMemory = CreateLongMemory(sqlClient, chatClient);

        // Pre-populate session messages with meaningful content for the LLM to extract from
        var sessionMemory = new SessionMemory(sqlClient, NullLoggerFactory.Instance);

        var sessionMessages = new List<ChatMessage>
        {
            CreateMsg("user",
                "Hi, my name is Charlie. I work as a data scientist at Northwind. I love Python and R.",
                "msg-seed-1"),
            CreateMsg("assistant",
                "Nice to meet you, Charlie! Data science is a fascinating field.",
                "msg-seed-2"),
            CreateMsg("user",
                "I've been running a book club every Thursday night for 3 years. We read mostly sci-fi.",
                "msg-seed-3"),
            CreateMsg("assistant",
                "That's an impressive commitment! What's the best book you've read recently?",
                "msg-seed-4")
        };
        await sessionMemory.SaveMessagesAsync(DefaultSessionId, sessionMessages, TestContext.Current.CancellationToken);

        // Act — directly extract (properly awaited, no fire-and-forget)
        await longMemory.ExtractAndStoreAsync(
            DefaultOwnerKey,
            [DefaultSessionId],
            TestContext.Current.CancellationToken);

        // Assert — long memory entries should now exist
        var contextMessages = await longMemory.GetContextMessagesAsync(
            DefaultOwnerKey, TestContext.Current.CancellationToken);

        Assert.NotEmpty(contextMessages);
        Assert.All(contextMessages, cm => Assert.Equal(ChatRole.System, cm.Role));

        // At least one entry should reference Charlie's info
        var allText = string.Join(" ", contextMessages.Select(m => m.Text));
        Assert.True(
            allText.Contains("Charlie", StringComparison.OrdinalIgnoreCase)
            || allText.Contains("data scientist", StringComparison.OrdinalIgnoreCase)
            || allText.Contains("book club", StringComparison.OrdinalIgnoreCase)
            || allText.Contains("Northwind", StringComparison.OrdinalIgnoreCase),
            $"Expected extracted entries to reference seeded content. Got: {allText}");
    }

    #endregion

    #region Error fallback — real deps

    /// <summary>
    /// Integration test: when the LongMemory SQLite database connection is
    /// interrupted, the middleware falls back to the original messages and
    /// the real LLM still responds normally.
    /// </summary>
    [Fact]
    public async Task GetResponseAsync_LongMemoryError_FallsBackAndGetsRealResponse()
    {
        // Arrange
        var chatClient = CreateChatClient();

        // Create a SQLite client that we'll close to simulate an error
        var sqlClient = CreateSqliteClient();
        var longMemory = CreateLongMemory(sqlClient, chatClient);

        // Seed some entries so GetContextMessagesAsync has data to read
        await SeedLongMemoryAsync(longMemory, DefaultOwnerKey,
            "fact", "Alice is a software engineer.", ct: TestContext.Current.CancellationToken);

        var sut = CreateSut(chatClient, longMemory);

        // Close the connection to force a DB error during augmentation
        sqlClient.Close();

        var requestMsg = CreateMsg("user", "Say hi in exactly one word.", "msg-001");

        // Act — should NOT throw despite DB error
        var response = await sut.GetResponseAsync([requestMsg], ct: TestContext.Current.CancellationToken);

        // Assert — got a real response despite the DB failure
        Assert.NotEmpty(response.Messages);
        var assistantMsg = response.Messages.FirstOrDefault(m => m.Role == ChatRole.Assistant);
        Assert.NotNull(assistantMsg);
        Assert.False(string.IsNullOrWhiteSpace(assistantMsg.Text));
    }

    #endregion
}
