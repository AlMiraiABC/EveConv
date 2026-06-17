using EveConv.Abstraction.Memory;
using EveConv.Memory.Models;
using EveConv.Memory.Config;
using EveConv.Memory.Services;
using EveConv.Memory.Managers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace EveConv.Memory.Tests.Services;

/// <summary>
/// Unit tests for LLMLongMemoryExtractor.ExtractAsync.
/// </summary>
public class LLMLongMemoryExtractorTests
{
    private static IOptions<MemoryConfiguration> CreateOptions(float importanceThreshold = 0.5f)
    {
        return Options.Create(new MemoryConfiguration { ImportanceThreshold = importanceThreshold });
    }

    private static IChatClient CreateMockChatClient(string responseText)
    {
        var mock = new Moq.Mock<IChatClient>();
        mock.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));
        return mock.Object;
    }

    private static IChatClient CreateThrowingChatClient(Exception ex)
    {
        var mock = new Moq.Mock<IChatClient>();
        mock.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(ex);
        return mock.Object;
    }

    #region ExtractAsync

    [Fact]
    public async Task ExtractAsync_NullSessionContents_ReturnsEmpty()
    {
        var extractor = new LLMLongMemoryExtractor(
            CreateMockChatClient("[]"),
            CreateOptions());

        var result = await extractor.ExtractAsync(null!, ["s1"], TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ExtractAsync_EmptySessionContents_ReturnsEmpty()
    {
        var extractor = new LLMLongMemoryExtractor(
            CreateMockChatClient("[]"),
            CreateOptions());

        var result = await extractor.ExtractAsync([], ["s1"], TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ExtractAsync_ValidJsonResponse_ReturnsParsedEntries()
    {
        var responseJson = """
        [
            {"category":"preference","content":"Likes Python","importance":0.9},
            {"category":"fact","content":"Works at Contoso","importance":0.85}
        ]
        """;
        var extractor = new LLMLongMemoryExtractor(
            CreateMockChatClient(responseJson),
            CreateOptions());

        var result = await extractor.ExtractAsync(
            ["User: I love Python"],
            ["session-1"],
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Equal("preference", result[0].Category);
        Assert.Equal("Likes Python", result[0].Content);
        Assert.True(result[0].Importance >= 0.5f);
        Assert.Equal("fact", result[1].Category);
        Assert.Equal("Works at Contoso", result[1].Content);
    }

    [Fact]
    public async Task ExtractAsync_JsonWithSurroundingText_ExtractsJsonArray()
    {
        var responseText = """
        Here are the extracted entries:
        ```json
        [
            {"category":"habit","content":"Drinks coffee every morning","importance":0.75}
        ]
        ```
        """;
        var extractor = new LLMLongMemoryExtractor(
            CreateMockChatClient(responseText),
            CreateOptions());

        var result = await extractor.ExtractAsync(
            ["User: I drink coffee every morning"],
            ["session-1"],
            TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("habit", result[0].Category);
        Assert.Equal("Drinks coffee every morning", result[0].Content);
        Assert.Equal(0.75f, result[0].Importance);
    }

    [Fact]
    public async Task ExtractAsync_ResponseWithoutJsonArray_ReturnsEmpty()
    {
        var responseText = "I couldn't find any relevant information to extract.";
        var extractor = new LLMLongMemoryExtractor(
            CreateMockChatClient(responseText),
            CreateOptions());

        var result = await extractor.ExtractAsync(
            ["Some conversation"],
            ["session-1"],
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ExtractAsync_EntriesBelowImportanceThreshold_AreFilteredOut()
    {
        var responseJson = """
        [
            {"category":"preference","content":"Likes dark mode","importance":0.9},
            {"category":"habit","content":"Sometimes uses light mode","importance":0.2},
            {"category":"fact","content":"Uses VS Code","importance":0.8}
        ]
        """;
        var extractor = new LLMLongMemoryExtractor(
            CreateMockChatClient(responseJson),
            CreateOptions(importanceThreshold: 0.5f));

        var result = await extractor.ExtractAsync(
            ["User: I use VS Code with dark mode"],
            ["session-1"],
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.True(e.Importance >= 0.5f));
        Assert.DoesNotContain(result, e => e.Category == "habit");
    }

    [Fact]
    public async Task ExtractAsync_EmptyJsonArray_ReturnsEmpty()
    {
        var extractor = new LLMLongMemoryExtractor(
            CreateMockChatClient("[]"),
            CreateOptions());

        var result = await extractor.ExtractAsync(
            ["Some conversation"],
            ["session-1"],
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ExtractAsync_MalformedJson_ReturnsEmpty()
    {
        var extractor = new LLMLongMemoryExtractor(
            CreateMockChatClient("[{invalid json}]"),
            CreateOptions());

        var result = await extractor.ExtractAsync(
            ["Some conversation"],
            ["session-1"],
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ExtractAsync_ChatClientThrowsException_ReturnsEmpty()
    {
        var extractor = new LLMLongMemoryExtractor(
            CreateThrowingChatClient(new InvalidOperationException("Model unavailable")),
            CreateOptions());

        var result = await extractor.ExtractAsync(
            ["Some conversation"],
            ["session-1"],
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ExtractAsync_EntriesHaveSourceSessionIds()
    {
        var responseJson = """
        [
            {"category":"preference","content":"Likes tea","importance":0.7}
        ]
        """;
        var sourceIds = new[] { "session-a", "session-b" };
        var extractor = new LLMLongMemoryExtractor(
            CreateMockChatClient(responseJson),
            CreateOptions());

        var result = await extractor.ExtractAsync(
            ["User: I like tea"],
            sourceIds,
            TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal(sourceIds, result[0].SourceSessionIds);
    }

    [Fact]
    public async Task ExtractAsync_EntriesHaveUniqueIds()
    {
        var responseJson = """
        [
            {"category":"fact","content":"Fact A","importance":0.6},
            {"category":"fact","content":"Fact B","importance":0.7},
            {"category":"fact","content":"Fact C","importance":0.8}
        ]
        """;
        var extractor = new LLMLongMemoryExtractor(
            CreateMockChatClient(responseJson),
            CreateOptions());

        var result = await extractor.ExtractAsync(
            ["Some conversation"],
            ["session-1"],
            TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Count);
        var ids = result.Select(e => e.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task ExtractAsync_EntriesHaveTimestampsSet()
    {
        var responseJson = """
        [
            {"category":"event","content":"Started new project","importance":0.9}
        ]
        """;
        var before = DateTimeOffset.UtcNow;
        var extractor = new LLMLongMemoryExtractor(
            CreateMockChatClient(responseJson),
            CreateOptions());

        var result = await extractor.ExtractAsync(
            ["User: I started a new project"],
            ["session-1"],
            TestContext.Current.CancellationToken);
        var after = DateTimeOffset.UtcNow;

        Assert.Single(result);
        Assert.InRange(result[0].CreatedAt, before, after);
        Assert.InRange(result[0].LastReinforcedAt, before, after);
    }

    [Fact]
    public async Task ExtractAsync_NullResponseText_ReturnsEmpty()
    {
        var mock = new Moq.Mock<IChatClient>();
        mock.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, (string)null!)));
        var extractor = new LLMLongMemoryExtractor(
            mock.Object,
            CreateOptions());

        var result = await extractor.ExtractAsync(
            ["Some conversation"],
            ["session-1"],
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ExtractAsync_BlankCategoryOrContent_AreFilteredOut()
    {
        var responseJson = """
        [
            {"category":"preference","content":"Valid entry","importance":0.9},
            {"category":"  ","content":"Blank category","importance":0.8},
            {"category":"habit","content":"   ","importance":0.7}
        ]
        """;
        var extractor = new LLMLongMemoryExtractor(
            CreateMockChatClient(responseJson),
            CreateOptions());

        var result = await extractor.ExtractAsync(
            ["Some conversation"],
            ["session-1"],
            TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("preference", result[0].Category);
        Assert.Equal("Valid entry", result[0].Content);
    }

    [Fact]
    public async Task ExtractAsync_OwnerKeyDefaultsToDefault()
    {
        var responseJson = """
        [
            {"category":"fact","content":"Test fact","importance":0.6}
        ]
        """;
        var extractor = new LLMLongMemoryExtractor(
            CreateMockChatClient(responseJson),
            CreateOptions());

        var result = await extractor.ExtractAsync(
            ["Some conversation"],
            ["session-1"],
            TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("default", result[0].OwnerKey);
    }

    [Fact]
    public async Task ExtractAsync_MultipleSessionContents_AreInjectedIntoPrompt()
    {
        string? capturedPrompt = null;
        var mock = new Moq.Mock<IChatClient>();
        mock.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((messages, _, _) =>
            {
                capturedPrompt = messages.First().Text;
            })
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "[]")));

        var extractor = new LLMLongMemoryExtractor(
            mock.Object,
            CreateOptions());

        var contents = new[] { "Session A content", "Session B content" };
        await extractor.ExtractAsync(contents, ["s1", "s2"], TestContext.Current.CancellationToken);

        Assert.NotNull(capturedPrompt);
        Assert.Contains("Session A content", capturedPrompt);
        Assert.Contains("Session B content", capturedPrompt);
    }

    #endregion

    #region MergeAsync

    private static LongMemoryEntry CreateEntry(
        string category = "fact",
        string content = "test content",
        float importance = 0.8f) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        OwnerKey = "default",
        Category = category,
        Content = content,
        Importance = importance,
        SourceSessionIds = ["old-session"],
        CreatedAt = DateTimeOffset.UtcNow,
        LastReinforcedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task MergeAsync_EmptySessionContents_ReturnsExistingEntries()
    {
        var extractor = new LLMLongMemoryExtractor(
            CreateMockChatClient("[]"),
            CreateOptions());

        var existing = new List<LongMemoryEntry> { CreateEntry(content: "Existing fact") };
        var result = await extractor.MergeAsync(existing, [], ["s1"], TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("Existing fact", result[0].Content);
    }

    [Fact]
    public async Task MergeAsync_NullSessionContents_ReturnsExistingEntries()
    {
        var extractor = new LLMLongMemoryExtractor(
            CreateMockChatClient("[]"),
            CreateOptions());

        var existing = new List<LongMemoryEntry> { CreateEntry(content: "Untouched") };
        var result = await extractor.MergeAsync(existing, null!, ["s1"], TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("Untouched", result[0].Content);
    }

    [Fact]
    public async Task MergeAsync_NoExistingEntries_ReturnsNewEntries()
    {
        var responseJson = """
        [
            {"category":"fact","content":"User is a developer","importance":0.9}
        ]
        """;
        var extractor = new LLMLongMemoryExtractor(
            CreateMockChatClient(responseJson),
            CreateOptions());

        var result = await extractor.MergeAsync(
            [], ["User: I am a developer"], ["s1"], TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("fact", result[0].Category);
        Assert.Equal("User is a developer", result[0].Content);
    }

    [Fact]
    public async Task MergeAsync_KeepsExistingEntries()
    {
        var responseJson = """
        [
            {"category":"fact","content":"Still works at Contoso","importance":0.85},
            {"category":"preference","content":"Likes dark mode","importance":0.9}
        ]
        """;
        var extractor = new LLMLongMemoryExtractor(
            CreateMockChatClient(responseJson),
            CreateOptions());

        var existing = new List<LongMemoryEntry>
        {
            CreateEntry(category: "fact", content: "Still works at Contoso", importance: 0.85f),
            CreateEntry(category: "preference", content: "Likes dark mode", importance: 0.9f)
        };

        var result = await extractor.MergeAsync(
            existing, ["User: I still use dark mode"], ["s1"], TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.Content == "Still works at Contoso");
        Assert.Contains(result, e => e.Content == "Likes dark mode");
    }

    [Fact]
    public async Task MergeAsync_UpdatesExistingEntry()
    {
        var responseJson = """
        [
            {"category":"fact","content":"Works at Fabrikam now","importance":0.9}
        ]
        """;
        var extractor = new LLMLongMemoryExtractor(
            CreateMockChatClient(responseJson),
            CreateOptions());

        var existing = new List<LongMemoryEntry>
        {
            CreateEntry(category: "fact", content: "Works at Contoso", importance: 0.7f)
        };

        var result = await extractor.MergeAsync(
            existing, ["User: I changed jobs to Fabrikam"], ["s1"], TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("Works at Fabrikam now", result[0].Content);
        Assert.True(result[0].Importance > 0.7f);
    }

    [Fact]
    public async Task MergeAsync_DeletesEntryByOmitting()
    {
        var responseJson = """
        [
            {"category":"preference","content":"Likes Python","importance":0.9}
        ]
        """;
        var extractor = new LLMLongMemoryExtractor(
            CreateMockChatClient(responseJson),
            CreateOptions());

        var existing = new List<LongMemoryEntry>
        {
            CreateEntry(category: "preference", content: "Likes Python", importance: 0.9f),
            CreateEntry(category: "habit", content: "Used to drink soda", importance: 0.6f)
        };

        // LLM omits "Used to drink soda" because user now says they quit
        var result = await extractor.MergeAsync(
            existing, ["User: I quit drinking soda"], ["s1"], TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("Likes Python", result[0].Content);
        Assert.DoesNotContain(result, e => e.Content.Contains("soda"));
    }

    [Fact]
    public async Task MergeAsync_AddsNewEntries()
    {
        var responseJson = """
        [
            {"category":"fact","content":"Lives in Seattle","importance":0.8},
            {"category":"habit","content":"Runs 5km daily","importance":0.85}
        ]
        """;
        var extractor = new LLMLongMemoryExtractor(
            CreateMockChatClient(responseJson),
            CreateOptions());

        var existing = new List<LongMemoryEntry>
        {
            CreateEntry(category: "fact", content: "Lives in Seattle", importance: 0.8f)
        };

        var result = await extractor.MergeAsync(
            existing, ["User: I started running 5km every day"], ["s1"], TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.Category == "habit" && e.Content == "Runs 5km daily");
    }

    [Fact]
    public async Task MergeAsync_ChatClientThrows_ReturnsExistingUnchanged()
    {
        var extractor = new LLMLongMemoryExtractor(
            CreateThrowingChatClient(new InvalidOperationException("Model unavailable")),
            CreateOptions());

        var existing = new List<LongMemoryEntry>
        {
            CreateEntry(content: "Safe entry", importance: 0.8f)
        };

        var result = await extractor.MergeAsync(
            existing, ["Some conversation"], ["s1"], TestContext.Current.CancellationToken);

        // Error path: returns existing entries unchanged
        Assert.Single(result);
        Assert.Equal("Safe entry", result[0].Content);
    }

    [Fact]
    public async Task MergeAsync_ResponseWithoutJsonArray_ReturnsExisting()
    {
        var extractor = new LLMLongMemoryExtractor(
            CreateMockChatClient("I'm sorry, I cannot process this request."),
            CreateOptions());

        var existing = new List<LongMemoryEntry>
        {
            CreateEntry(content: "Preserved entry", importance: 0.8f)
        };

        var result = await extractor.MergeAsync(
            existing, ["Some conversation"], ["s1"], TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("Preserved entry", result[0].Content);
    }

    [Fact]
    public async Task MergeAsync_NewEntriesHaveSourceSessionIds()
    {
        var responseJson = """
        [
            {"category":"fact","content":"New fact","importance":0.8}
        ]
        """;
        var sourceIds = new[] { "session-x", "session-y" };
        var extractor = new LLMLongMemoryExtractor(
            CreateMockChatClient(responseJson),
            CreateOptions());

        var result = await extractor.MergeAsync(
            [], ["User: Some content"], sourceIds, TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal(sourceIds, result[0].SourceSessionIds);
    }

    [Fact]
    public async Task MergeAsync_MergesSemanticallySimilarEntries()
    {
        var responseJson = """
        [
            {"category":"preference","content":"Loves Go for backend development","importance":0.95}
        ]
        """;
        var extractor = new LLMLongMemoryExtractor(
            CreateMockChatClient(responseJson),
            CreateOptions());

        var existing = new List<LongMemoryEntry>
        {
            CreateEntry(category: "preference", content: "Likes Go for its simplicity", importance: 0.7f),
            CreateEntry(category: "preference", content: "Prefers Go for backend", importance: 0.6f)
        };

        // LLM should merge the two similar entries into one with higher importance
        var result = await extractor.MergeAsync(
            existing, ["User: I really love Go for all my backend work"], ["s1"], TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Contains("Go", result[0].Content);
        Assert.True(result[0].Importance >= 0.9f);
    }

    #endregion
}
