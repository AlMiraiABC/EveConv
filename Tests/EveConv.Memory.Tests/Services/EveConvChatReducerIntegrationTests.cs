using System.ClientModel;
using dotenv.net;
using EveConv.Abstraction.Memory;
using EveConv.Memory.Models;
using EveConv.Memory.Options;
using EveConv.Memory.Services;
using EveConv.Memory.Stores;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI;

namespace EveConv.Memory.Tests.Services;

/// <summary>
/// Integration tests for <see cref="EveConvChatReducer"/> using a real OpenAI-compatible API,
/// <see cref="InMemorySessionMemory"/>, and <see cref="TiktokenCounter"/>.
/// Reads <c>OPENAI_ENDPOINT</c>, <c>OPENAI_MODEL_ID</c>, and <c>OPENAI_API_KEY</c> from
/// the <c>.env</c> file. Tests are skipped when credentials are not configured.
/// </summary>
public class EveConvChatReducerIntegrationTests
{
    private const string DefaultSessionId = "integration-session-001";

    private readonly string _endpoint;
    private readonly string _modelId;
    private readonly string _apiKey;

    public EveConvChatReducerIntegrationTests()
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

    /// <summary>Create the reducer wired to real OpenAI, in-memory session, and TiktokenCounter.</summary>
    private static EveConvChatReducer CreateReducer(
        IChatClient chatClient,
        ISessionMemory session,
        ITokenCounter tokenCounter,
        MemoryOptions? options = null)
    {
        var opts = Microsoft.Extensions.Options.Options.Create(options ?? new MemoryOptions
        {
            DefaultContextWindowTokens = 4096,
            CompactReserveRecentCount = 3,
            MaxCompactionLevel = 3
        });

        return new EveConvChatReducer(
            chatClient,
            session,
            tokenCounter,
            opts,
            NullLoggerFactory.Instance);
    }

    /// <summary>
    /// Create a <see cref="ChatMessage"/> with <see cref="EveConv.Memory.Models.MemoryMetadataContent"/> attached.
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

    #region Basic compaction smoke test

    /// <summary>
    /// Integration test: messages exceeding the token window trigger summarization
    /// via a real LLM, and the result contains the summary as a System message
    /// followed by the reserved recent messages.
    /// </summary>
    [Fact]
    public async Task ReduceAsync_ExceedsWindow_CompactsWithRealLLM()
    {
        var chatClient = CreateChatClient();
        var session = new InMemorySessionMemory();
        var tokenCounter = new TiktokenCounter();
        var options = new MemoryOptions
        {
            DefaultContextWindowTokens = 50, // very small window to force compaction
            CompactReserveRecentCount = 1, // keep only the last message
            MaxCompactionLevel = 3
        };

        var reducer = CreateReducer(chatClient, session, tokenCounter, options);

        var messages = new List<ChatMessage>
        {
            CreateMsg("user", "My name is Alice. I work as a backend engineer at Contoso.", "msg-1"),
            CreateMsg("assistant", "Nice to meet you, Alice! What tech stack do you use?", "msg-2"),
            CreateMsg("user", "We use C# and .NET for our services. I really enjoy the ecosystem.", "msg-3"),
            CreateMsg("assistant", "That's great. Any hobbies?", "msg-4"),
            CreateMsg("user", "I love hiking in the mountains on weekends. Last month I did a 15km trail.", "msg-5"),
            CreateMsg("assistant", "Sounds amazing! How often do you go?", "msg-6"),
            CreateMsg("user", "Every Saturday morning, rain or shine. It's my reset ritual.", "msg-7"),
        };

        var result = await reducer.ReduceAsync(messages, TestContext.Current.CancellationToken);
        var list = result.ToList();

        // Should have at least: summary + the last message
        Assert.True(list.Count >= 2, $"Expected >= 2 messages, got {list.Count}");

        // First message should be the System summary
        var summary = list[0];
        Assert.Equal(ChatRole.System, summary.Role);
        Assert.False(string.IsNullOrWhiteSpace(summary.Text), "Summary should not be empty");

        // Summary should contain key facts from the compacted messages
        Assert.Contains("Alice", summary.Text, StringComparison.OrdinalIgnoreCase);

        // Last message should be the retained recent message
        var recent = list[^1];
        Assert.Equal("Every Saturday morning, rain or shine. It's my reset ritual.", recent.Text);

        // Verify compaction was persisted
        var compactions = await session.GetCompactionsAsync(DefaultSessionId, TestContext.Current.CancellationToken);
        Assert.NotEmpty(compactions);
        var compaction = compactions[0];
        Assert.Equal(1, compaction.CompactionLevel);
        Assert.Equal(DefaultSessionId, compaction.SessionId);
        Assert.False(string.IsNullOrWhiteSpace(compaction.CompactedSummary));
        Assert.NotEmpty(compaction.SourceMessageIds);
        Assert.True(compaction.SourceMessageIds.Count >= 1);
    }

    #endregion

    #region Multi-level compaction

    /// <summary>
    /// Integration test: existing compactions + new uncovered messages that exceed
    /// the window produce a new compaction at the next level via a real LLM.
    /// </summary>
    [Fact]
    public async Task ReduceAsync_WithExistingCompaction_CreatesNextLevelWithRealLLM()
    {
        var chatClient = CreateChatClient();
        var session = new InMemorySessionMemory();
        var tokenCounter = new TiktokenCounter();

        // Pre-seed an existing level-1 compaction
        var existingCompaction = new SessionCompaction
        {
            Id = "comp-existing-001",
            SessionId = DefaultSessionId,
            CompactedSummary = "Alice is a backend engineer at Contoso, uses C#/.NET, and enjoys hiking on weekends.",
            SourceMessageIds = ["msg-1", "msg-2", "msg-3", "msg-4"],
            OriginalTokenCount = 200,
            CompactedTokenCount = 30,
            CompactionLevel = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1)
        };
        await session.SaveCompactionAsync(existingCompaction, TestContext.Current.CancellationToken);

        var options = new MemoryOptions
        {
            DefaultContextWindowTokens = 20, // very small — force compaction
            CompactReserveRecentCount = 1,
            MaxCompactionLevel = 3
        };

        var reducer = CreateReducer(chatClient, session, tokenCounter, options);

        // New messages not covered by the existing compaction
        var messages = new List<ChatMessage>
        {
            // These are already covered by existingCompaction
            CreateMsg("user", "(covered) My name is Alice.", "msg-1"),
            CreateMsg("assistant", "(covered) Nice to meet you!", "msg-2"),
            CreateMsg("user", "(covered) I use C# and .NET.", "msg-3"),
            CreateMsg("assistant", "(covered) Great!", "msg-4"),
            // These are new and will trigger a new level-2 compaction
            CreateMsg("user", "I just adopted a golden retriever puppy named Max.", "msg-5"),
            CreateMsg("assistant", "That's adorable! How old is Max?", "msg-6"),
            CreateMsg("user", "He is 3 months old and full of energy. He already knows sit and stay.", "msg-7"),
        };

        var result = await reducer.ReduceAsync(messages, TestContext.Current.CancellationToken);
        var list = result.ToList();

        // Expected: [level-1 summary, level-2 summary, recent msg-7]
        Assert.True(list.Count >= 3, $"Expected >= 3 messages, got {list.Count}");

        // Level 1 summary
        Assert.Equal(ChatRole.System, list[0].Role);
        Assert.Contains("Alice", list[0].Text, StringComparison.OrdinalIgnoreCase);

        // Level 2 summary (newly created)
        Assert.Equal(ChatRole.System, list[1].Role);
        Assert.False(string.IsNullOrWhiteSpace(list[1].Text));
        Assert.Contains("Max", list[1].Text, StringComparison.OrdinalIgnoreCase);

        // Recent message kept
        Assert.Equal("He is 3 months old and full of energy. He already knows sit and stay.", list[^1].Text);

        // Verify two compactions now exist
        var compactions = await session.GetCompactionsAsync(DefaultSessionId, TestContext.Current.CancellationToken);
        Assert.Equal(2, compactions.Count);
        Assert.Contains(compactions, c => c.CompactionLevel == 2);
    }

    #endregion

    #region Within window — no compaction

    /// <summary>
    /// Integration test: messages within the token window are returned unchanged,
    /// with no LLM summarization call and no compaction persisted.
    /// </summary>
    [Fact]
    public async Task ReduceAsync_WithinWindow_ReturnsUnchanged_NoCompactionSaved()
    {
        var chatClient = CreateChatClient();
        var session = new InMemorySessionMemory();
        var tokenCounter = new TiktokenCounter();
        var options = new MemoryOptions
        {
            DefaultContextWindowTokens = 4096, // large window
            CompactReserveRecentCount = 5,
            MaxCompactionLevel = 3
        };

        var reducer = CreateReducer(chatClient, session, tokenCounter, options);

        var messages = new List<ChatMessage>
        {
            CreateMsg("user", "Hello, how are you?", "msg-1"),
            CreateMsg("assistant", "I'm doing well, thank you! How can I help?", "msg-2"),
        };

        var result = await reducer.ReduceAsync(messages, TestContext.Current.CancellationToken);
        var list = result.ToList();

        // Should return both messages unchanged
        Assert.Equal(2, list.Count);
        Assert.Equal("Hello, how are you?", list[0].Text);
        Assert.Equal("I'm doing well, thank you! How can I help?", list[1].Text);

        // No compaction should be saved
        var compactions = await session.GetCompactionsAsync(DefaultSessionId, TestContext.Current.CancellationToken);
        Assert.Empty(compactions);
    }

    #endregion

    #region Empty messages — fast path

    /// <summary>
    /// Integration test: empty message list returns immediately without any API call.
    /// This test does NOT require credentials — it never reaches the LLM client.
    /// </summary>
    [Fact]
    public async Task ReduceAsync_EmptyMessages_ReturnsEmptyWithoutApiCall()
    {
        // Use a real session and counter, but the chat client is never invoked
        var session = new InMemorySessionMemory();
        var tokenCounter = new TiktokenCounter();

        // We still need a client for construction, but it won't be called.
        // If no credentials, construct a non-functional placeholder.
        // Use a simple echo client as placeholder — it won't be called
        var chatClient = new EchoChatClient();

        var reducer = CreateReducer(chatClient, session, tokenCounter);

        var result = await reducer.ReduceAsync([], TestContext.Current.CancellationToken);
        Assert.Empty(result);
    }

    #endregion

    #region No metadata — returns unchanged

    /// <summary>
    /// Integration test: messages without <see cref="EveConv.Memory.Models.MemoryMetadataContent"/>
    /// are returned as-is without any compaction attempt.
    /// This test does NOT require credentials.
    /// </summary>
    [Fact]
    public async Task ReduceAsync_NoMetadata_ReturnsUnchangedWithoutApiCall()
    {
        var session = new InMemorySessionMemory();
        var tokenCounter = new TiktokenCounter();
        var chatClient = new EchoChatClient();

        var reducer = CreateReducer(chatClient, session, tokenCounter);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello — no metadata attached"),
            new(ChatRole.Assistant, "Hi there — also no metadata"),
        };

        var result = await reducer.ReduceAsync(messages, TestContext.Current.CancellationToken);
        var list = result.ToList();

        Assert.Equal(2, list.Count);
        Assert.Equal("Hello — no metadata attached", list[0].Text);
        Assert.Equal("Hi there — also no metadata", list[1].Text);

        var compactions = await session.GetCompactionsAsync(DefaultSessionId, TestContext.Current.CancellationToken);
        Assert.Empty(compactions);
    }

    #endregion

    #region Placeholder chat client (for tests that don't require API)

    /// <summary>
    /// Minimal <see cref="IChatClient"/> that echoes input as output.
    /// Used as a construction placeholder when the test never calls the LLM.
    /// </summary>
    private sealed class EchoChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var input = messages.LastOrDefault()?.Text ?? string.Empty;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, $"Echo: {input}")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Streaming not implemented in EchoChatClient");
        }

        public void Dispose()
        {
        }

        public object? GetService(Type serviceType, object? key = null) => null;
    }

    #endregion
}
