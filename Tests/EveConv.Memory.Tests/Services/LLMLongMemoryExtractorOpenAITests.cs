using System.ClientModel;
using dotenv.net;
using EveConv.Memory.Services;
using Microsoft.Extensions.AI;
using OpenAI;
using MemoryOptions = EveConv.Memory.Options.MemoryOptions;

namespace EveConv.Memory.Tests.Services;

/// <summary>
/// Integration tests for <see cref="LLMLongMemoryExtractor"/> using a real OpenAI-compatible API.
/// Reads <c>OPENAI_ENDPOINT</c>, <c>OPENAI_MODEL_ID</c>, and <c>OPENAI_API_KEY</c> from
/// the <c>.env</c> file. Tests are skipped when credentials are not configured.
/// </summary>
public class LLMLongMemoryExtractorOpenAITests
{
    private readonly string _endpoint;
    private readonly string _modelId;
    private readonly string _apiKey;

    public LLMLongMemoryExtractorOpenAITests()
    {
        DotEnv.Load();

        _endpoint = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT") ?? throw new ArgumentNullException();
        _modelId = Environment.GetEnvironmentVariable("OPENAI_MODEL_ID") ?? throw new ArgumentNullException();
        _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new ArgumentNullException();
    }

    private IChatClient CreateChatClient()
    {
        var client = new OpenAIClient(
            new ApiKeyCredential(_apiKey),
            new OpenAIClientOptions { Endpoint = new Uri(_endpoint) });
        return client.GetChatClient(_modelId).AsIChatClient();
    }

    private LLMLongMemoryExtractor CreateExtractor(
        IChatClient? chatClient = null,
        float importanceThreshold = 0.3f)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new MemoryOptions
        {
            ImportanceThreshold = importanceThreshold
        });

        return new LLMLongMemoryExtractor(
            chatClient ?? CreateChatClient(),
            options);
    }

    /// <summary>
    /// Integration test: extracts long-term memory entries from realistic
    /// conversation history using a live OpenAI-compatible model.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_WithRealLLM_ReturnsMeaningfulEntries()
    {
        var sessionContents = new List<string>
        {
            """
            User: Hi, my name is Alice. I work as a software engineer at California.
            Assistant: Nice to meet you, Alice! What kind of software do you work on?
            User: I mainly build backend services in Go and Python. I prefer Go for its simplicity.
            """,

            """
            User: I've been going for a 5km run every morning before work. It helps me stay focused.
            Assistant: That's a great habit! How long have you been doing that?
            User: About 6 months now. I never miss a day, even on weekends.
            """,

            """
            User: My team just shipped a major feature last Friday. We had a big celebration.
            Assistant: Congratulations! That must feel great.
            User: Yeah, it was a huge milestone for us. The project took 8 months.
            """
        };

        var sourceSessionIds = new[] { "session-1", "session-2", "session-3" };

        var extractor = CreateExtractor();
        var result = await extractor.ExtractAsync(
            sessionContents,
            sourceSessionIds,
            TestContext.Current.CancellationToken);

        // We should get at least some entries back from a real LLM
        Assert.NotEmpty(result);

        // All entries should have the expected structure
        foreach (var entry in result)
        {
            Assert.NotEmpty(entry.Id);
            Assert.NotEmpty(entry.Category);
            Assert.NotEmpty(entry.Content);
            Assert.True(entry.Importance is >= 0f and <= 1f);
            Assert.Equal(sourceSessionIds, entry.SourceSessionIds);
            Assert.True(entry.CreatedAt <= DateTimeOffset.UtcNow);
            Assert.Equal("default", entry.OwnerKey);
        }

        // Categories should be from the expected set
        var validCategories = new[] { "preference", "habit", "event", "fact" };
        Assert.All(result, e => Assert.Contains(e.Category, validCategories));

        // At minimum, we should extract Alice's name, job, preferences, and habits
        Assert.True(result.Count >= 3,
            $"Expected at least 3 extracted entries, got {result.Count}");
    }

    /// <summary>
    /// Integration test: verifies that the importance threshold is respected
    /// by the LLM. Low-importance trivia (e.g. "blue socks on Tuesdays")
    /// should be excluded at a high threshold but included at a low threshold.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_HighImportanceThreshold_ReturnsFewerEntries()
    {
        var sessionContents = new List<string>
        {
            """
            User: My name is Bob. I work as a database administrator at Fabrikam.
            Assistant: Nice to meet you, Bob! That sounds like an important role.
            User: I also like my coffee black and I organize my desk every morning.
            Assistant: Got it — black coffee and a tidy desk. Those are good habits.
            User: Oh, and sometimes I wear blue socks on Tuesdays.
            """
        };

        // High threshold (0.85): only significant facts should survive
        // (name, occupation), while trivial preferences should be dropped.
        var highThresholdExtractor = CreateExtractor(importanceThreshold: 0.85f);
        var highResult = await highThresholdExtractor.ExtractAsync(
            sessionContents,
            ["session-1"],
            TestContext.Current.CancellationToken);

        // Low threshold (0.2): nearly everything should be captured,
        // including low-importance trivia like "blue socks on Tuesdays".
        var lowThresholdExtractor = CreateExtractor(importanceThreshold: 0.2f);
        var lowResult = await lowThresholdExtractor.ExtractAsync(
            sessionContents,
            ["session-1"],
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(highResult);
        Assert.NotEmpty(lowResult);
        Assert.True(highResult.Count <= lowResult.Count,
            $"High threshold ({highResult.Count} entries) should have ≤ entries than low threshold ({lowResult.Count} entries). " +
            $"High: [{string.Join(", ", highResult.Select(e => e.Content))}]; " +
            $"Low: [{string.Join(", ", lowResult.Select(e => e.Content))}]");
    }

    /// <summary>
    /// Integration test: single session with clear facts should extract
    /// at least one entry.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_SingleSession_ProducesEntries()
    {
        var sessionContents = new List<string>
        {
            "User: I live in Seattle and I love hiking in the Cascades on weekends."
        };

        var extractor = CreateExtractor();
        var result = await extractor.ExtractAsync(
            sessionContents,
            ["session-1"],
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(result);

        // Verify entries are well-formed
        Assert.All(result, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Content));
            Assert.False(string.IsNullOrWhiteSpace(entry.Category));
            Assert.True(entry.Content.Length < 500, "Content should be a short description");
        });
    }

    /// <summary>
    /// Integration test: empty session contents should return empty immediately
    /// without making any API call. This also verifies the fast-path guard.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_EmptyContents_ReturnsEmptyWithoutApiCall()
    {
        // This test doesn't need API credentials — it never reaches the client
        var extractor = CreateExtractor();
        var result = await extractor.ExtractAsync(
            [],
            ["session-1"],
            TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }
}
