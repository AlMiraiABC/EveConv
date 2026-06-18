using EveConv.Abstraction.Cache;
using EveConv.Abstraction.Memory;
using EveConv.Memory.Config;
using EveConv.Memory.Managers;
using EveConv.Memory.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace EveConv.Memory.Tests.Managers;

/// <summary>
/// Unit tests for <see cref="RecentMemory"/> using mocked <see cref="ICache"/>
/// and <see cref="ISessionMemory"/>.
/// </summary>
public class RecentMemoryTests
{
    private const string DefaultSessionId = "session-1";

    #region helpers

    private static Mock<ICache> CreateCacheMock()
    {
        return new Mock<ICache>();
    }

    private static Mock<ISessionMemory> CreateSessionMock()
    {
        return new Mock<ISessionMemory>();
    }

    private static MemoryConfiguration CreateConfig(int recentCount = 20, int cacheTtlHours = 1)
    {
        return new MemoryConfiguration
        {
            RecentMemoryCount = recentCount,
            RecentMemoryCacheTtl = TimeSpan.FromHours(cacheTtlHours)
        };
    }

    private static RecentMemory CreateSut(
        Mock<ICache>? cacheMock = null,
        Mock<ISessionMemory>? sessionMock = null,
        MemoryConfiguration? config = null)
    {
        cacheMock ??= CreateCacheMock();
        sessionMock ??= CreateSessionMock();
        config ??= CreateConfig();

        return new RecentMemory(
            cacheMock.Object,
            sessionMock.Object,
            Options.Create(config),
            NullLoggerFactory.Instance);
    }

    /// <summary>
    /// Creates a <see cref="ChatMessage"/> with <see cref="MemoryMetadataContent"/> attached.
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

    /// <summary>
    /// Creates a <see cref="ChatMessage"/> without <see cref="MemoryMetadataContent"/>.
    /// </summary>
    private static ChatMessage CreateMessageWithoutMetadata(string text)
    {
        return new ChatMessage(ChatRole.User, text);
    }

    /// <summary>
    /// Captures objects pushed to the cache list for later inspection.
    /// </summary>
    private static List<object> CapturePushedItems(Mock<ICache> cacheMock, string cacheKey)
    {
        var captured = new List<object>();
        cacheMock
            .Setup(c => c.ListRightPushAsync(
                cacheKey,
                It.IsAny<object>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object, TimeSpan?, CancellationToken>((_, value, _, _) =>
            {
                captured.Add(value);
            })
            .ReturnsAsync((string _, object _, TimeSpan? _, CancellationToken _) => captured.Count);
        return captured;
    }

    #endregion

    #region GetRecentAsync

    [Fact]
    public async Task GetRecentAsync_CacheHit_ReturnsCachedMessages()
    {
        var cacheMock = CreateCacheMock();
        var sessionMock = CreateSessionMock();
        IList<object> cachedMessages = new List<object>
        {
            CreateMessage("Cached msg 1"),
            CreateMessage("Cached msg 2")
        };

        cacheMock
            .Setup(c => c.ListRangeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedMessages);

        var sut = CreateSut(cacheMock, sessionMock);

        var result = await sut.GetRecentAsync(DefaultSessionId, 10, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Contains("Cached msg 1", result[0].Text);
        Assert.Contains("Cached msg 2", result[1].Text);

        // Session (DB) should NOT be called on cache hit
        sessionMock.Verify(
            s => s.GetMessagesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetRecentAsync_CacheMiss_LoadsFromDbAndBackfills()
    {
        var cacheMock = CreateCacheMock();
        var sessionMock = CreateSessionMock();

        // Cache returns empty
        cacheMock
            .Setup(c => c.ListRangeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IList<object>)[]);

        IReadOnlyList<ChatMessage> dbMessages = new List<ChatMessage>
        {
            CreateMessage("DB msg 1"),
            CreateMessage("DB msg 2"),
            CreateMessage("DB msg 3"),
            CreateMessage("DB msg 4"),
            CreateMessage("DB msg 5")
        };
        sessionMock
            .Setup(s => s.GetMessagesAsync(DefaultSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbMessages);

        // Capture pushed items for verification
        var pushedItems = CapturePushedItems(cacheMock, "recent:session-1");

        var sut = CreateSut(cacheMock, sessionMock, CreateConfig(recentCount: 20));

        var result = await sut.GetRecentAsync(DefaultSessionId, 3, TestContext.Current.CancellationToken);

        // Should return last 3 messages
        Assert.Equal(3, result.Count);
        Assert.Contains("DB msg 3", result[0].Text);
        Assert.Contains("DB msg 4", result[1].Text);
        Assert.Contains("DB msg 5", result[2].Text);

        // Cache should be backfilled with last 3 messages
        Assert.Equal(3, pushedItems.Count);
    }

    [Fact]
    public async Task GetRecentAsync_CacheMiss_DbEmpty_ReturnsEmpty()
    {
        var cacheMock = CreateCacheMock();
        var sessionMock = CreateSessionMock();

        cacheMock
            .Setup(c => c.ListRangeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IList<object>)[]);

        sessionMock
            .Setup(s => s.GetMessagesAsync(DefaultSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = CreateSut(cacheMock, sessionMock);

        var result = await sut.GetRecentAsync(DefaultSessionId, 10, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetRecentAsync_CacheMiss_DbFewerThanCount_ReturnsAllAvailable()
    {
        var cacheMock = CreateCacheMock();
        var sessionMock = CreateSessionMock();

        cacheMock
            .Setup(c => c.ListRangeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IList<object>)[]);

        sessionMock
            .Setup(s => s.GetMessagesAsync(DefaultSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateMessage("Only one")]);

        var sut = CreateSut(cacheMock, sessionMock);

        var result = await sut.GetRecentAsync(DefaultSessionId, 10, TestContext.Current.CancellationToken);

        Assert.Single(result);
    }

    [Fact]
    public async Task GetRecentAsync_CacheReturnsNull_HandlesGracefully()
    {
        var cacheMock = CreateCacheMock();
        var sessionMock = CreateSessionMock();

        cacheMock
            .Setup(c => c.ListRangeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IList<object>)[]);

        sessionMock
            .Setup(s => s.GetMessagesAsync(DefaultSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateMessage("Fallback msg")]);

        var sut = CreateSut(cacheMock, sessionMock);

        var result = await sut.GetRecentAsync(DefaultSessionId, 5, TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Contains("Fallback msg", result[0].Text);
    }

    #endregion

    #region PushMessageAsync

    [Fact]
    public async Task PushMessageAsync_MessageWithoutMetadata_AttachesMetadata()
    {
        var cacheMock = CreateCacheMock();
        var sessionMock = CreateSessionMock();
        var config = CreateConfig(recentCount: 5);

        // Setup cache to return length 1 (no trim needed)
        cacheMock
            .Setup(c => c.ListRightPushAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var sut = CreateSut(cacheMock, sessionMock, config);
        var message = CreateMessageWithoutMetadata("Test message");

        await sut.PushMessageAsync(DefaultSessionId, message, TestContext.Current.CancellationToken);

        // Verify metadata was attached
        var metadata = message.Contents.OfType<MemoryMetadataContent>().FirstOrDefault();
        Assert.NotNull(metadata);
        Assert.Equal(DefaultSessionId, metadata.SessionId);
        Assert.NotNull(metadata.MessageId);
        Assert.NotEmpty(metadata.MessageId);

        // Verify DB save was called
        sessionMock.Verify(
            s => s.SaveMessagesAsync(
                DefaultSessionId,
                It.Is<IEnumerable<ChatMessage>>(msgs => msgs.Contains(message)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PushMessageAsync_MessageWithMetadata_PreservesExistingMetadata()
    {
        var cacheMock = CreateCacheMock();
        var sessionMock = CreateSessionMock();
        var config = CreateConfig(recentCount: 5);

        cacheMock
            .Setup(c => c.ListRightPushAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var sut = CreateSut(cacheMock, sessionMock, config);
        var message = CreateMessage("Existing metadata msg", messageId: "preset-id");

        await sut.PushMessageAsync(DefaultSessionId, message, TestContext.Current.CancellationToken);

        var metadata = message.Contents.OfType<MemoryMetadataContent>().FirstOrDefault();
        Assert.NotNull(metadata);
        Assert.Equal("preset-id", metadata.MessageId);
    }

    [Fact]
    public async Task PushMessageAsync_ExceedsMaxCount_TrimsExcess()
    {
        var cacheMock = CreateCacheMock();
        var sessionMock = CreateSessionMock();
        var config = CreateConfig(recentCount: 3);

        // Setup: length after push = 4 (exceeds max of 3)
        cacheMock
            .Setup(c => c.ListRightPushAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        var sut = CreateSut(cacheMock, sessionMock, config);
        var message = CreateMessage("Overflow msg");

        await sut.PushMessageAsync(DefaultSessionId, message, TestContext.Current.CancellationToken);

        // Verify ListLeftPopAsync was called with excess = 1
        cacheMock.Verify(
            c => c.ListLeftPopAsync("recent:session-1", 1, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PushMessageAsync_WithinLimit_DoesNotTrim()
    {
        var cacheMock = CreateCacheMock();
        var sessionMock = CreateSessionMock();
        var config = CreateConfig(recentCount: 5);

        cacheMock
            .Setup(c => c.ListRightPushAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3); // length = 3, max = 5 -> no trim

        var sut = CreateSut(cacheMock, sessionMock, config);
        var message = CreateMessage("Within limit");

        await sut.PushMessageAsync(DefaultSessionId, message, TestContext.Current.CancellationToken);

        cacheMock.Verify(
            c => c.ListLeftPopAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PushMessageAsync_PushesToCorrectCacheKey()
    {
        var cacheMock = CreateCacheMock();
        var sessionMock = CreateSessionMock();
        var config = CreateConfig(recentCount: 5);

        cacheMock
            .Setup(c => c.ListRightPushAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var sut = CreateSut(cacheMock, sessionMock, config);
        var message = CreateMessage("Key check");

        await sut.PushMessageAsync("custom-session", message, TestContext.Current.CancellationToken);

        // Verify the cache key includes the correct session ID
        cacheMock.Verify(
            c => c.ListRightPushAsync(
                "recent:custom-session",
                It.IsAny<object>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region TrimAsync

    [Fact]
    public async Task TrimAsync_LengthLessThanMax_DoesNothing()
    {
        var cacheMock = CreateCacheMock();
        var sessionMock = CreateSessionMock();

        cacheMock
            .Setup(c => c.ListLengthAsync("recent:session-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var sut = CreateSut(cacheMock, sessionMock);

        await sut.TrimAsync(DefaultSessionId, 10, TestContext.Current.CancellationToken);

        // No pop since 5 <= 10
        cacheMock.Verify(
            c => c.ListLeftPopAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TrimAsync_LengthEqualsMax_DoesNothing()
    {
        var cacheMock = CreateCacheMock();
        var sessionMock = CreateSessionMock();

        cacheMock
            .Setup(c => c.ListLengthAsync("recent:session-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        var sut = CreateSut(cacheMock, sessionMock);

        await sut.TrimAsync(DefaultSessionId, 10, TestContext.Current.CancellationToken);

        cacheMock.Verify(
            c => c.ListLeftPopAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TrimAsync_LengthExceedsMax_PopsExcess()
    {
        var cacheMock = CreateCacheMock();
        var sessionMock = CreateSessionMock();

        cacheMock
            .Setup(c => c.ListLengthAsync("recent:session-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(15); // 15 total, max = 10 -> pop 5

        var sut = CreateSut(cacheMock, sessionMock);

        await sut.TrimAsync(DefaultSessionId, 10, TestContext.Current.CancellationToken);

        cacheMock.Verify(
            c => c.ListLeftPopAsync("recent:session-1", 5, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TrimAsync_UsesCorrectCacheKey()
    {
        var cacheMock = CreateCacheMock();
        var sessionMock = CreateSessionMock();

        cacheMock
            .Setup(c => c.ListLengthAsync("recent:custom-session", It.IsAny<CancellationToken>()))
            .ReturnsAsync(20);

        var sut = CreateSut(cacheMock, sessionMock);

        await sut.TrimAsync("custom-session", 10, TestContext.Current.CancellationToken);

        cacheMock.Verify(
            c => c.ListLeftPopAsync("recent:custom-session", 10, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
