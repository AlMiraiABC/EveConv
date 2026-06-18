using dotenv.net;
using EveConv.Abstraction.Cache;
using EveConv.Abstraction.Memory;
using EveConv.Cache.InMemory;
using EveConv.Memory.Config;
using EveConv.Memory.Managers;
using EveConv.Memory.Middleware;
using EveConv.Memory.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;

namespace EveConv.Memory.Tests.Middleware;

/// <summary>
/// Integration tests for <see cref="MemoryPersistingChatClient"/> using
/// a real OpenAI-compatible API, <see cref="InMemoryCache"/>,
/// <see cref="InMemorySessionMemory"/>, and <see cref="RecentMemory"/>.
/// Reads <c>OPENAI_ENDPOINT</c>, <c>OPENAI_MODEL_ID</c>, and <c>OPENAI_API_KEY</c>
/// from the <c>.env</c> file.
/// </summary>
public class MemoryPersistingChatClientIntegrationTests
{
    private const string DefaultSessionId = "integration-session-001";

    private readonly string _endpoint;
    private readonly string _modelId;
    private readonly string _apiKey;

    public MemoryPersistingChatClientIntegrationTests()
    {
        DotEnv.Load();

        _endpoint = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT")
                    ?? throw new ArgumentNullException();
        _modelId = Environment.GetEnvironmentVariable("OPENAI_MODEL_ID")
                   ?? throw new ArgumentNullException();
        _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                  ?? throw new ArgumentNullException();
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

    /// <summary>Create an <see cref="InMemoryCache"/> for testing.</summary>
    private static InMemoryCache CreateCache()
    {
        return new InMemoryCache(
            Options.Create(new InMemoryConfiguration()),
            NullLoggerFactory.Instance);
    }

    /// <summary>Create an <see cref="InMemorySessionMemory"/> for testing.</summary>
    private static InMemorySessionMemory CreateSessionMemory()
    {
        return new InMemorySessionMemory();
    }

    /// <summary>Create a <see cref="RecentMemory"/> wired to in-memory storage.</summary>
    private static RecentMemory CreateRecentMemory(ICache cache, ISessionMemory session)
    {
        var config = new MemoryConfiguration
        {
            RecentMemoryCount = 50,
            RecentMemoryCacheTtl = TimeSpan.FromHours(1)
        };

        return new RecentMemory(cache, session, Options.Create(config), NullLoggerFactory.Instance);
    }

    /// <summary>
    /// Creates a <see cref="ChatMessage"/> with <see cref="MemoryMetadataContent"/> attached.
    /// </summary>
    private static ChatMessage CreateMsg(string role, string text, string messageId, string? sessionId = null)
    {
        var msg = new ChatMessage(new ChatRole(role), text);
        msg.Contents.Add(new MemoryMetadataContent
        {
            MessageId = messageId,
            SessionId = sessionId ?? DefaultSessionId
        });
        return msg;
    }

    #endregion

    #region Full pipeline: non-streaming with real OpenAI

    /// <summary>
    /// Integration test: send a message through the middleware with a real LLM,
    /// verify the response, and confirm the messages were persisted to recent memory.
    /// </summary>
    [Fact]
    public async Task FullPipeline_SendMessage_PersistsAndReturnsRealResponse()
    {
        // Arrange
        var cache = CreateCache();
        var session = CreateSessionMemory();
        var recentMemory = CreateRecentMemory(cache, session);
        var chatClient = CreateChatClient();

        var sut = new MemoryPersistingChatClient(chatClient, recentMemory, NullLoggerFactory.Instance);

        var requestMsg = CreateMsg("user", "Say hello in exactly three words.", "msg-001");

        // Act
        var response = await sut.GetResponseAsync([requestMsg], ct: TestContext.Current.CancellationToken);

        // Assert - response from real LLM
        Assert.NotEmpty(response.Messages);
        Assert.Contains(response.Messages, m =>
            m.Role == ChatRole.Assistant &&
            !string.IsNullOrWhiteSpace(m.Text));

        // Assert - request and response messages persisted to recent memory
        var recent = await recentMemory.GetRecentAsync(DefaultSessionId, 10, TestContext.Current.CancellationToken);
        Assert.Contains(recent, m =>
            m.Role == ChatRole.User &&
            m.Text == "Say hello in exactly three words.");

        foreach (var responseMessage in response.Messages.Where(m =>
                     m.Role == ChatRole.Assistant &&
                     !string.IsNullOrWhiteSpace(m.Text)))
        {
            Assert.Contains(recent, m => m.Role == ChatRole.Assistant && m.Text == responseMessage.Text);
        }
    }

    /// <summary>
    /// Integration test: multi-turn conversation with a real LLM,
    /// verifying all turns are persisted in order.
    /// </summary>
    [Fact]
    public async Task FullPipeline_MultiTurn_PersistsAllTurns()
    {
        // Arrange
        var cache = CreateCache();
        var session = CreateSessionMemory();
        var recentMemory = CreateRecentMemory(cache, session);
        var chatClient = CreateChatClient();

        var sut = new MemoryPersistingChatClient(chatClient, recentMemory, NullLoggerFactory.Instance);

        var memoryCode = $"MEM-{Guid.NewGuid():N}";

        // Turn 1
        var turn1Text = $"Remember this code exactly: {memoryCode}";
        var turn1Msg = CreateMsg("user", turn1Text, "msg-001");
        var response1 = await sut.GetResponseAsync([turn1Msg], ct: TestContext.Current.CancellationToken);
        Assert.NotEmpty(response1.Messages);

        // Turn 2 - include history
        var turn2Text = "What code did I ask you to remember?";
        var turn2Msg = CreateMsg("user", turn2Text, "msg-002");
        var turn2Messages = new List<ChatMessage> { turn1Msg, response1.Messages[0], turn2Msg };
        var response2 = await sut.GetResponseAsync(turn2Messages, ct: TestContext.Current.CancellationToken);
        Assert.NotEmpty(response2.Messages);

        // Assert - LLM should remember from conversation history
        Assert.Contains(response2.Messages, m =>
            m.Role == ChatRole.Assistant &&
            !string.IsNullOrWhiteSpace(m.Text) &&
            m.Text.Contains(memoryCode, StringComparison.Ordinal));

        // Assert - key user turns persisted in expected order
        var recent = await recentMemory.GetRecentAsync(DefaultSessionId, 10, TestContext.Current.CancellationToken);
        var userTurnIndexes = recent
            .Select((m, index) => (Message: m, Index: index))
            .Where(x => x.Message.Role == ChatRole.User)
            .ToList();

        var turn1Indexes = userTurnIndexes
            .Where(x => x.Message.Text == turn1Text)
            .Select(x => x.Index)
            .ToList();
        var turn2Indexes = userTurnIndexes
            .Where(x => x.Message.Text == turn2Text)
            .Select(x => x.Index)
            .ToList();

        Assert.Equal(2, turn1Indexes.Count);
        Assert.Single(turn2Indexes);
        Assert.True(turn1Indexes[0] < turn1Indexes[1]);
        Assert.True(turn1Indexes[1] < turn2Indexes[0]);
    }

    /// <summary>
    /// Integration test: multiple sessions remain isolated
    /// in recent memory storage.
    /// </summary>
    [Fact]
    public async Task FullPipeline_MultipleSessions_IsolatedInRecentMemory()
    {
        // Arrange
        var cache = CreateCache();
        var session = CreateSessionMemory();
        var recentMemory = CreateRecentMemory(cache, session);
        var chatClient = CreateChatClient();

        var sut = new MemoryPersistingChatClient(chatClient, recentMemory, NullLoggerFactory.Instance);

        const string sessionA = "integration-session-a";
        const string sessionB = "integration-session-b";

        // Session A
        var msgA = CreateMsg("user", "This is session A.", "msg-a1", sessionA);
        await sut.GetResponseAsync([msgA], ct: TestContext.Current.CancellationToken);

        // Session B
        var msgB = CreateMsg("user", "This is session B.", "msg-b1", sessionB);
        await sut.GetResponseAsync([msgB], ct: TestContext.Current.CancellationToken);

        // Assert - each session has its own messages
        var recentA = await recentMemory.GetRecentAsync(sessionA, 10, TestContext.Current.CancellationToken);
        var recentB = await recentMemory.GetRecentAsync(sessionB, 10, TestContext.Current.CancellationToken);

        Assert.Contains(recentA, m => m.Role == ChatRole.User && m.Text == "This is session A.");
        Assert.Contains(recentB, m => m.Role == ChatRole.User && m.Text == "This is session B.");
        Assert.DoesNotContain(recentA, m => m.Role == ChatRole.User && m.Text == "This is session B.");
        Assert.DoesNotContain(recentB, m => m.Role == ChatRole.User && m.Text == "This is session A.");
    }

    #endregion

    #region Full pipeline: streaming with real OpenAI

    /// <summary>
    /// Integration test: streaming response from a real LLM,
    /// verifying the synthesized response is persisted.
    /// </summary>
    [Fact]
    public async Task FullPipeline_Streaming_PersistsSynthesizedResponse()
    {
        // Arrange
        var cache = CreateCache();
        var session = CreateSessionMemory();
        var recentMemory = CreateRecentMemory(cache, session);
        var chatClient = CreateChatClient();

        var sut = new MemoryPersistingChatClient(chatClient, recentMemory, NullLoggerFactory.Instance);

        var requestMsg = CreateMsg("user", "Count from 1 to 3, one number per line.", "msg-001");

        // Act
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in sut.GetStreamingResponseAsync(
                           [requestMsg], ct: TestContext.Current.CancellationToken))
        {
            updates.Add(update);
        }

        // Assert - streaming produced output
        Assert.NotEmpty(updates);

        var fullText = string.Concat(updates.Where(u => u.Text is not null).Select(u => u.Text));
        Assert.False(string.IsNullOrWhiteSpace(fullText));

        // Assert - persisted to recent memory (user + synthesized assistant)
        var recent = await recentMemory.GetRecentAsync(DefaultSessionId, 10, TestContext.Current.CancellationToken);
        Assert.Contains(recent, m => m.Role == ChatRole.User && m.Text == "Count from 1 to 3, one number per line.");
        Assert.Contains(recent, m => m.Role == ChatRole.Assistant && m.Text == fullText);
    }

    #endregion

    #region Metadata handling

    /// <summary>
    /// Integration test: messages without metadata get metadata attached
    /// during the persistence pipeline.
    /// </summary>
    [Fact]
    public async Task FullPipeline_MissingMetadata_AttachedDuringPersistence()
    {
        // Arrange
        var cache = CreateCache();
        var session = CreateSessionMemory();
        var recentMemory = CreateRecentMemory(cache, session);
        var chatClient = CreateChatClient();

        var sut = new MemoryPersistingChatClient(chatClient, recentMemory, NullLoggerFactory.Instance);

        // Message with metadata (provides sessionId)
        var msgWith = CreateMsg("user", "Message with metadata", "msg-001");
        // Message without metadata
        var msgWithout = new ChatMessage(ChatRole.User, "Message without metadata");

        // Act
        await sut.GetResponseAsync([msgWith, msgWithout], ct: TestContext.Current.CancellationToken);

        // Assert - metadata was attached to the message that lacked it
        var attached = msgWithout.Contents.OfType<MemoryMetadataContent>().FirstOrDefault();
        Assert.NotNull(attached);
        Assert.Equal(DefaultSessionId, attached.SessionId);
        Assert.NotEmpty(attached.MessageId);
    }

    #endregion
}
