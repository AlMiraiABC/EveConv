using EveConv.Abstraction.Memory;
using EveConv.Memory.Models;
using EveConv.Memory.Config;
using EveConv.Memory.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace EveConv.Memory.Tests.Services;

/// <summary>
/// Unit tests for <see cref="EveConvChatReducer"/> covering all compaction paths,
/// early-return conditions, multi-level compaction, and error handling.
/// </summary>
public class EveConvChatReducerTests
{
    private const string DefaultSessionId = "session-001";
    private const int DefaultWindowTokens = 4096;
    private const int DefaultReserveRecent = 5;
    private const int DefaultMaxLevel = 3;

    #region helpers

    /// <summary>Create a <see cref="ChatMessage"/> with <see cref="MemoryMetadataContent"/> attached.</summary>
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

    /// <summary>Create default <see cref="MemoryConfiguration"/> for tests.</summary>
    private static MemoryConfiguration CreateOptions(
        int windowTokens = DefaultWindowTokens,
        int reserveRecent = DefaultReserveRecent,
        int maxLevel = DefaultMaxLevel)
        => new()
        {
            DefaultContextWindowTokens = windowTokens,
            CompactReserveRecentCount = reserveRecent,
            MaxCompactionLevel = maxLevel
        };

    /// <summary>Create the reducer with mocked dependencies.</summary>
    private static (EveConvChatReducer Sut, Mock<IChatClient> ChatClient, Mock<ISessionMemory> Session,
        Mock<ITokenCounter> TokenCounter)
        CreateSut(MemoryConfiguration? options = null)
    {
        var chatClient = new Mock<IChatClient>();
        var session = new Mock<ISessionMemory>();
        var tokenCounter = new Mock<ITokenCounter>();
        var opts = Options.Create(options ?? CreateOptions());

        var sut = new EveConvChatReducer(
            chatClient.Object,
            session.Object,
            tokenCounter.Object,
            opts,
            NullLoggerFactory.Instance);

        return (sut, chatClient, session, tokenCounter);
    }

    #endregion

    #region ReduceAsync – empty / null inputs

    [Fact]
    public async Task ReduceAsync_EmptyMessages_ReturnsEmpty()
    {
        var (sut, _, _, _) = CreateSut();

        var result = await sut.ReduceAsync([], TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ReduceAsync_NullMessages_ReturnsEmpty()
    {
        var (sut, _, _, _) = CreateSut();

        var result = await sut.ReduceAsync(null!, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    #endregion

    #region ReduceAsync – no metadata

    [Fact]
    public async Task ReduceAsync_MessagesWithoutMetadata_ReturnsUnchanged()
    {
        var (sut, _, _, _) = CreateSut();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        var result = await sut.ReduceAsync(messages, TestContext.Current.CancellationToken);

        var list = result.ToList();
        Assert.Single(list);
        Assert.Equal("Hello", list[0].Text);
    }

    #endregion

    #region ReduceAsync – within token window (no compaction)

    [Fact]
    public async Task ReduceAsync_WithinTokenWindow_ReturnsModelHistoryWithoutCompacting()
    {
        var (sut, _, session, tokenCounter) = CreateSut();
        var messages = new List<ChatMessage>
        {
            CreateMsg("user", "Hello", "msg-1"),
            CreateMsg("assistant", "Hi there", "msg-2"),
        };

        session
            .Setup(s => s.GetCompactionsAsync(DefaultSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Token count below window → no compaction
        tokenCounter
            .Setup(t => t.CountTokens(It.IsAny<IReadOnlyList<ChatMessage>>()))
            .Returns(100);

        var result = await sut.ReduceAsync(messages, TestContext.Current.CancellationToken);

        var list = result.ToList();
        Assert.Equal(2, list.Count);
        Assert.Equal("Hello", list[0].Text);
        Assert.Equal("Hi there", list[1].Text);

        // Must not save any compaction
        session.Verify(
            s => s.SaveCompactionAsync(It.IsAny<SessionCompaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region ReduceAsync – exceeds window → compaction triggered

    [Fact]
    public async Task ReduceAsync_ExceedsTokenWindow_CompactsOlderMessages()
    {
        var options = CreateOptions(reserveRecent: 1);
        var (sut, chatClient, session, tokenCounter) = CreateSut(options);
        var messages = new List<ChatMessage>
        {
            CreateMsg("user", "Old message 1", "msg-1"),
            CreateMsg("user", "Old message 2", "msg-2"),
            CreateMsg("user", "Recent message", "msg-3"),
        };

        session
            .Setup(s => s.GetCompactionsAsync(DefaultSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // First token count (modelHistory) exceeds window
        tokenCounter
            .Setup(t => t.CountTokens(It.IsAny<IReadOnlyList<ChatMessage>>()))
            .Returns(5000);

        // Token count for summary text
        tokenCounter
            .Setup(t => t.CountTokens(It.IsAny<string>()))
            .Returns(20);

        // Mock summarization
        chatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                "Summarized: Old message 1, Old message 2")));

        var result = await sut.ReduceAsync(messages, TestContext.Current.CancellationToken);

        var list = result.ToList();
        Assert.Equal(2, list.Count);

        // First item: the new compaction summary
        Assert.Equal(ChatRole.System, list[0].Role);
        Assert.Contains("Summarized", list[0].Text, StringComparison.Ordinal);

        // Second item: the recent message that was kept
        Assert.Equal("Recent message", list[1].Text);

        // Verify compaction was persisted with correct data
        session.Verify(
            s => s.SaveCompactionAsync(
                It.Is<SessionCompaction>(c =>
                    c.SessionId == DefaultSessionId &&
                    c.CompactionLevel == 1 &&
                    c.SourceMessageIds.Count == 2 &&
                    c.SourceMessageIds.Contains("msg-1") &&
                    c.SourceMessageIds.Contains("msg-2")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region ReduceAsync – already-compacted messages excluded

    [Fact]
    public async Task ReduceAsync_AlreadyCompactedMessages_ExcludedAndSummaryPrepended()
    {
        var options = CreateOptions(reserveRecent: 1);
        var (sut, _, session, tokenCounter) = CreateSut(options);

        var existingCompaction = new SessionCompaction
        {
            Id = "comp-1",
            SessionId = DefaultSessionId,
            CompactedSummary = "Previous summary of msg-1 and msg-2.",
            SourceMessageIds = ["msg-1", "msg-2"],
            OriginalTokenCount = 100,
            CompactedTokenCount = 10,
            CompactionLevel = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

        var messages = new List<ChatMessage>
        {
            CreateMsg("user", "Old message 1", "msg-1"), // already compacted
            CreateMsg("user", "Old message 2", "msg-2"), // already compacted
            CreateMsg("user", "New message 3", "msg-3"), // uncovered → compact
            CreateMsg("user", "Recent message 4", "msg-4"), // uncovered → keep
        };

        session
            .Setup(s => s.GetCompactionsAsync(DefaultSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([existingCompaction]);

        // modelHistory tokens: exceed window
        tokenCounter
            .Setup(t => t.CountTokens(It.IsAny<IReadOnlyList<ChatMessage>>()))
            .Returns(5000);
        tokenCounter
            .Setup(t => t.CountTokens(It.IsAny<string>()))
            .Returns(15);

        var result = await sut.ReduceAsync(messages, TestContext.Current.CancellationToken);

        var list = result.ToList();
        // Expected: [existing summary (level 1), new summary (level 2), recent msg-4]
        Assert.Equal(3, list.Count);
        Assert.Equal(ChatRole.System, list[0].Role);
        Assert.Contains("Previous summary", list[0].Text, StringComparison.Ordinal);
        Assert.Equal(ChatRole.System, list[1].Role);
        Assert.Equal("Recent message 4", list[2].Text);
    }

    #endregion

    #region ReduceAsync – nothing to compact (all within reserve)

    [Fact]
    public async Task ReduceAsync_AllMessagesInReserve_ReturnsModelHistory()
    {
        var options = CreateOptions(reserveRecent: 5);
        var (sut, _, session, tokenCounter) = CreateSut(options);
        var messages = new List<ChatMessage>
        {
            CreateMsg("user", "Msg A", "msg-1"),
            CreateMsg("user", "Msg B", "msg-2"),
            CreateMsg("user", "Msg C", "msg-3"),
        };

        session
            .Setup(s => s.GetCompactionsAsync(DefaultSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Exceeds window to trigger the compaction path
        tokenCounter
            .Setup(t => t.CountTokens(It.IsAny<IReadOnlyList<ChatMessage>>()))
            .Returns(5000);

        var result = await sut.ReduceAsync(messages, TestContext.Current.CancellationToken);

        // All 3 messages are within reserve count (5), so nothing is compacted
        var list = result.ToList();
        Assert.Equal(3, list.Count);

        session.Verify(
            s => s.SaveCompactionAsync(It.IsAny<SessionCompaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region ReduceAsync – max compaction level reached

    [Fact]
    public async Task ReduceAsync_MaxCompactionLevelReached_SkipsCompaction()
    {
        var options = CreateOptions(reserveRecent: 1, maxLevel: 1);
        var (sut, _, session, tokenCounter) = CreateSut(options);

        var existingCompaction = new SessionCompaction
        {
            Id = "comp-1",
            SessionId = DefaultSessionId,
            CompactedSummary = "Existing level 1 summary.",
            SourceMessageIds = ["msg-0"],
            OriginalTokenCount = 50,
            CompactedTokenCount = 5,
            CompactionLevel = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

        var messages = new List<ChatMessage>
        {
            CreateMsg("user", "Msg A", "msg-1"),
            CreateMsg("user", "Msg B", "msg-2"),
        };

        session
            .Setup(s => s.GetCompactionsAsync(DefaultSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([existingCompaction]);

        tokenCounter
            .Setup(t => t.CountTokens(It.IsAny<IReadOnlyList<ChatMessage>>()))
            .Returns(5000);

        var result = await sut.ReduceAsync(messages, TestContext.Current.CancellationToken);

        var list = result.ToList();
        // modelHistory returned as-is — existing summary + both messages
        Assert.Equal(3, list.Count);
        Assert.Equal(ChatRole.System, list[0].Role);
        Assert.Contains("Existing level 1 summary", list[0].Text, StringComparison.Ordinal);

        // No new compaction saved
        session.Verify(
            s => s.SaveCompactionAsync(It.IsAny<SessionCompaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region ReduceAsync – summarization failure fallback

    [Fact]
    public async Task ReduceAsync_SummarizationThrows_CompactsWithFallbackMessage()
    {
        var options = CreateOptions(reserveRecent: 1);
        var (sut, chatClient, session, tokenCounter) = CreateSut(options);
        var messages = new List<ChatMessage>
        {
            CreateMsg("user", "Old msg", "msg-1"),
            CreateMsg("user", "Recent msg", "msg-2"),
        };

        session
            .Setup(s => s.GetCompactionsAsync(DefaultSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        tokenCounter
            .Setup(t => t.CountTokens(It.IsAny<IReadOnlyList<ChatMessage>>()))
            .Returns(5000);
        tokenCounter
            .Setup(t => t.CountTokens(It.IsAny<string>()))
            .Returns(10);

        // Simulate summarization failure
        chatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM unavailable"));

        var result = await sut.ReduceAsync(messages, TestContext.Current.CancellationToken);

        var list = result.ToList();
        Assert.Equal(2, list.Count);

        // The fallback summary is the first item
        Assert.Equal(ChatRole.System, list[0].Role);
        Assert.Contains("Summary unavailable", list[0].Text, StringComparison.Ordinal);

        // Recent message kept
        Assert.Equal("Recent msg", list[1].Text);
    }

    #endregion

    #region ReduceAsync – multi-level compaction with empty uncovered

    [Fact]
    public async Task ReduceAsync_OnlyExistingCompactionsNoUncovered_ReturnsSummariesOnly()
    {
        var (sut, _, session, tokenCounter) = CreateSut();

        var compactions = new List<SessionCompaction>
        {
            new()
            {
                Id = "comp-1", SessionId = DefaultSessionId,
                CompactedSummary = "Level 1 summary.",
                SourceMessageIds = ["msg-1"],
                OriginalTokenCount = 50, CompactedTokenCount = 5,
                CompactionLevel = 1, CreatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = "comp-2", SessionId = DefaultSessionId,
                CompactedSummary = "Level 2 summary.",
                SourceMessageIds = ["msg-2"],
                OriginalTokenCount = 30, CompactedTokenCount = 4,
                CompactionLevel = 2, CreatedAt = DateTimeOffset.UtcNow
            }
        };

        // Messages whose IDs are all already covered
        var messages = new List<ChatMessage>
        {
            CreateMsg("user", "Covered msg 1", "msg-1"),
            CreateMsg("user", "Covered msg 2", "msg-2"),
        };

        session
            .Setup(s => s.GetCompactionsAsync(DefaultSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(compactions);

        tokenCounter
            .Setup(t => t.CountTokens(It.IsAny<IReadOnlyList<ChatMessage>>()))
            .Returns(100);

        var result = await sut.ReduceAsync(messages, TestContext.Current.CancellationToken);

        var list = result.ToList();
        // 2 existing summaries ordered by compaction level, no raw messages
        Assert.Equal(2, list.Count);
        Assert.Contains("Level 1 summary", list[0].Text, StringComparison.Ordinal);
        Assert.Contains("Level 2 summary", list[1].Text, StringComparison.Ordinal);
    }

    #endregion

    #region ReduceAsync – compactions correctly ordered by level

    [Fact]
    public async Task ReduceAsync_ExistingCompactions_OrderedByLevelInResult()
    {
        var options = CreateOptions(reserveRecent: 1, maxLevel: 4);
        var (sut, chatClient, session, tokenCounter) = CreateSut(options);

        // Existing compactions stored out of order
        var compactions = new List<SessionCompaction>
        {
            new()
            {
                Id = "comp-3", SessionId = DefaultSessionId,
                CompactedSummary = "Third summary.",
                SourceMessageIds = ["msg-3"],
                OriginalTokenCount = 30, CompactedTokenCount = 3,
                CompactionLevel = 3, CreatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = "comp-1", SessionId = DefaultSessionId,
                CompactedSummary = "First summary.",
                SourceMessageIds = ["msg-1"],
                OriginalTokenCount = 50, CompactedTokenCount = 5,
                CompactionLevel = 1, CreatedAt = DateTimeOffset.UtcNow.AddHours(-2)
            }
        };

        var messages = new List<ChatMessage>
        {
            CreateMsg("user", "New msg A", "msg-10"),
            CreateMsg("user", "New msg B", "msg-11"),
        };

        session
            .Setup(s => s.GetCompactionsAsync(DefaultSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(compactions);

        tokenCounter
            .Setup(t => t.CountTokens(It.IsAny<IReadOnlyList<ChatMessage>>()))
            .Returns(5000);
        tokenCounter
            .Setup(t => t.CountTokens(It.IsAny<string>()))
            .Returns(10);

        chatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "New level 4 summary.")));

        var result = await sut.ReduceAsync(messages, TestContext.Current.CancellationToken);

        var list = result.ToList();
        // [level 1 summary, level 3 summary, new level 4 summary, recent msg]
        Assert.Equal(4, list.Count);
        Assert.Contains("First summary", list[0].Text, StringComparison.Ordinal);
        Assert.Contains("Third summary", list[1].Text, StringComparison.Ordinal);
        Assert.Contains("New level 4 summary", list[2].Text, StringComparison.Ordinal);
        Assert.Equal("New msg B", list[3].Text);
    }
    
    #endregion
}
