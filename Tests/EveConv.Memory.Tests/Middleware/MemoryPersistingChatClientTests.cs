using EveConv.Abstraction.Memory;
using EveConv.Memory.Middleware;
using EveConv.Memory.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace EveConv.Memory.Tests.Middleware;

/// <summary>
/// Unit tests for <see cref="MemoryPersistingChatClient"/> using mocked
/// <see cref="IChatClient"/> and <see cref="IRecentMemory"/>.
/// Each test isolates a single path through the middleware.
/// </summary>
public class MemoryPersistingChatClientTests
{
    private const string DefaultSessionId = "session-unit-1";

    #region helpers

    private static Mock<IChatClient> CreateInnerMock()
    {
        return new Mock<IChatClient>();
    }

    private static Mock<IRecentMemory> CreateRecentMemoryMock()
    {
        return new Mock<IRecentMemory>();
    }

    private static MemoryPersistingChatClient CreateSut(
        Mock<IChatClient>? innerMock = null,
        Mock<IRecentMemory>? recentMemoryMock = null,
        ILoggerFactory? loggerFactory = null)
    {
        innerMock ??= CreateInnerMock();
        recentMemoryMock ??= CreateRecentMemoryMock();
        loggerFactory ??= NullLoggerFactory.Instance;

        return new MemoryPersistingChatClient(
            innerMock.Object,
            recentMemoryMock.Object,
            loggerFactory);
    }

    /// <summary>
    /// Creates a <see cref="ChatMessage"/> with <see cref="MemoryMetadataContent"/> attached.
    /// </summary>
    private static ChatMessage CreateMessage(
        string role,
        string text,
        string? messageId = null,
        string? sessionId = null)
    {
        var msg = new ChatMessage(new ChatRole(role), text);
        msg.Contents.Add(new MemoryMetadataContent
        {
            MessageId = messageId ?? Guid.NewGuid().ToString("N"),
            SessionId = sessionId ?? DefaultSessionId
        });
        return msg;
    }

    /// <summary>
    /// Creates a <see cref="ChatMessage"/> without <see cref="MemoryMetadataContent"/>.
    /// </summary>
    private static ChatMessage CreateMessageWithoutMetadata(string role, string text)
    {
        return new ChatMessage(new ChatRole(role), text);
    }

    /// <summary>
    /// Creates a simple <see cref="ChatResponse"/> with a single assistant message.
    /// </summary>
    private static ChatResponse CreateResponse(string text)
    {
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
    }

    /// <summary>
    /// Creates a <see cref="ChatResponse"/> with multiple messages.
    /// </summary>
    private static ChatResponse CreateMultiMessageResponse(params string[] texts)
    {
        var messages = texts.Select(t => new ChatMessage(ChatRole.Assistant, t)).ToList();
        return new ChatResponse(messages);
    }

    /// <summary>
    /// Creates an async enumerable simulating a streaming response.
    /// </summary>
    private static async IAsyncEnumerable<ChatResponseUpdate> CreateStreamingUpdates(
        string[] chunks)
    {
        foreach (var chunk in chunks)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, chunk);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Wraps a <see cref="List{T}"/> as an <see cref="IAsyncEnumerable{T}"/>.
    /// </summary>
    private static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(
        List<T> list)
    {
        foreach (var item in list)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Returns an empty <see cref="IAsyncEnumerable{T}"/>.
    /// </summary>
    private static async IAsyncEnumerable<T> EmptyAsync<T>()
    {
        await Task.CompletedTask;
        yield break;
    }

    /// <summary>
    /// Creates a list of <see cref="ChatResponseUpdate"/> instances with null text mixed in.
    /// </summary>
    private static List<ChatResponseUpdate> CreateUpdatesWithNullText()
    {
        return
        [
            new ChatResponseUpdate(ChatRole.Assistant, "Hello"),
            new ChatResponseUpdate(ChatRole.Assistant, (string)null!),
            new ChatResponseUpdate(ChatRole.Assistant, " World")
        ];
    }

    #endregion

    #region GetResponseAsync

    [Fact]
    public async Task GetResponseAsync_WithSessionId_PersistsIncomingAndResponse()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var recentMemoryMock = CreateRecentMemoryMock();

        var response = CreateResponse("Hello from AI");
        innerMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var sut = CreateSut(innerMock, recentMemoryMock);

        var requestMessages = new List<ChatMessage>
        {
            CreateMessage("user", "Hello")
        };

        // Act
        var result = await sut.GetResponseAsync(requestMessages, ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(response, result);

        // Incoming message should be persisted
        recentMemoryMock.Verify(
            r => r.PushMessageAsync(
                DefaultSessionId,
                It.Is<ChatMessage>(m => m.Text == "Hello"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Response message should be persisted
        recentMemoryMock.Verify(
            r => r.PushMessageAsync(
                DefaultSessionId,
                It.Is<ChatMessage>(m => m.Text == "Hello from AI"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Inner client was called with the messages
        innerMock.Verify(
            c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetResponseAsync_WithoutSessionId_SkipsPersistence_CallsInner()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var recentMemoryMock = CreateRecentMemoryMock();

        var response = CreateResponse("Hello from AI");
        innerMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var sut = CreateSut(innerMock, recentMemoryMock);

        var requestMessages = new List<ChatMessage>
        {
            CreateMessageWithoutMetadata("user", "Hello")
        };

        // Act
        var result = await sut.GetResponseAsync(requestMessages, ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(response, result);

        // No persistence should happen without sessionId
        recentMemoryMock.Verify(
            r => r.PushMessageAsync(
                It.IsAny<string>(),
                It.IsAny<ChatMessage>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        // Inner client still called
        innerMock.Verify(
            c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetResponseAsync_NullMessages_HandlesGracefully()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var recentMemoryMock = CreateRecentMemoryMock();

        var response = CreateResponse("Response to null");
        innerMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var sut = CreateSut(innerMock, recentMemoryMock);

        // Act - null messages should not throw
        var result = await sut.GetResponseAsync(null!, ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(response, result);
        innerMock.Verify(
            c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetResponseAsync_EmptyMessages_HandlesGracefully()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var recentMemoryMock = CreateRecentMemoryMock();

        var response = CreateResponse("Response to empty");
        innerMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var sut = CreateSut(innerMock, recentMemoryMock);

        // Act
        var result = await sut.GetResponseAsync([], ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(response, result);
        recentMemoryMock.Verify(
            r => r.PushMessageAsync(
                It.IsAny<string>(),
                It.IsAny<ChatMessage>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetResponseAsync_MultiMessageResponse_PersistsEachResponseMessage()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var recentMemoryMock = CreateRecentMemoryMock();

        var response = CreateMultiMessageResponse("Part 1", "Part 2", "Part 3");
        innerMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var sut = CreateSut(innerMock, recentMemoryMock);

        var requestMessages = new List<ChatMessage>
        {
            CreateMessage("user", "Tell me a story")
        };

        // Act
        await sut.GetResponseAsync(requestMessages, ct: TestContext.Current.CancellationToken);

        // Assert - each response message should be persisted
        recentMemoryMock.Verify(
            r => r.PushMessageAsync(
                DefaultSessionId,
                It.Is<ChatMessage>(m => m.Text == "Part 1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        recentMemoryMock.Verify(
            r => r.PushMessageAsync(
                DefaultSessionId,
                It.Is<ChatMessage>(m => m.Text == "Part 2"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        recentMemoryMock.Verify(
            r => r.PushMessageAsync(
                DefaultSessionId,
                It.Is<ChatMessage>(m => m.Text == "Part 3"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetResponseAsync_PassesOptionsAndCancellationToken()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var recentMemoryMock = CreateRecentMemoryMock();
        var response = CreateResponse("Ok");
        var options = new ChatOptions { Temperature = 0.5f };

        innerMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var sut = CreateSut(innerMock, recentMemoryMock);

        var requestMessages = new List<ChatMessage>
        {
            CreateMessage("user", "Hi")
        };
        
        // Act
        await sut.GetResponseAsync(requestMessages, options, TestContext.Current.CancellationToken);

        // Assert
        innerMock.Verify(
            c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                options,
                TestContext.Current.CancellationToken),
            Times.Once);
    }

    #endregion

    #region GetStreamingResponseAsync

    [Fact]
    public async Task GetStreamingResponseAsync_WithSessionId_PersistsIncomingAndResponse()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var recentMemoryMock = CreateRecentMemoryMock();

        var chunks = new[] { "Hello", " ", "World" };
        var updates = CreateStreamingUpdates(chunks);
        innerMock
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns(updates);

        var sut = CreateSut(innerMock, recentMemoryMock);

        var requestMessages = new List<ChatMessage>
        {
            CreateMessage("user", "Say hello")
        };

        // Act - consume the stream
        var receivedUpdates = new List<ChatResponseUpdate>();
        await foreach (var update in sut.GetStreamingResponseAsync(
            requestMessages, ct: TestContext.Current.CancellationToken))
        {
            receivedUpdates.Add(update);
        }

        // Assert
        Assert.Equal(3, receivedUpdates.Count);

        // Incoming message should be persisted
        recentMemoryMock.Verify(
            r => r.PushMessageAsync(
                DefaultSessionId,
                It.Is<ChatMessage>(m => m.Text == "Say hello"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Synthesized assistant response should be persisted
        recentMemoryMock.Verify(
            r => r.PushMessageAsync(
                DefaultSessionId,
                It.Is<ChatMessage>(m =>
                    m.Role == ChatRole.Assistant && m.Text == "Hello World"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_WithoutSessionId_SkipsPersistence_StreamsAll()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var recentMemoryMock = CreateRecentMemoryMock();

        var chunks = new[] { "Hello" };
        var updates = CreateStreamingUpdates(chunks);
        innerMock
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns(updates);

        var sut = CreateSut(innerMock, recentMemoryMock);

        var requestMessages = new List<ChatMessage>
        {
            CreateMessageWithoutMetadata("user", "Hi")
        };

        // Act
        var receivedUpdates = new List<ChatResponseUpdate>();
        await foreach (var update in sut.GetStreamingResponseAsync(
            requestMessages, ct: TestContext.Current.CancellationToken))
        {
            receivedUpdates.Add(update);
        }

        // Assert
        Assert.Single(receivedUpdates);

        // No persistence should happen without sessionId
        recentMemoryMock.Verify(
            r => r.PushMessageAsync(
                It.IsAny<string>(),
                It.IsAny<ChatMessage>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_EmptyStream_NoPersistenceOfResponse()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var recentMemoryMock = CreateRecentMemoryMock();

        innerMock
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns(EmptyAsync<ChatResponseUpdate>());

        var sut = CreateSut(innerMock, recentMemoryMock);

        var requestMessages = new List<ChatMessage>
        {
            CreateMessage("user", "Test")
        };

        // Act
        var receivedUpdates = new List<ChatResponseUpdate>();
        await foreach (var update in sut.GetStreamingResponseAsync(
            requestMessages, ct: TestContext.Current.CancellationToken))
        {
            receivedUpdates.Add(update);
        }

        // Assert
        Assert.Empty(receivedUpdates);

        // Incoming should still be persisted (sessionId present)
        recentMemoryMock.Verify(
            r => r.PushMessageAsync(
                DefaultSessionId,
                It.Is<ChatMessage>(m => m.Text == "Test"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // No additional push for response (empty stream)
        recentMemoryMock.Verify(
            r => r.PushMessageAsync(
                It.IsAny<string>(),
                It.IsAny<ChatMessage>(),
                It.IsAny<CancellationToken>()),
            Times.Once); // Only the incoming message
    }

    [Fact]
    public async Task GetStreamingResponseAsync_NullTextChunks_ExcludedFromPersistedResponse()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var recentMemoryMock = CreateRecentMemoryMock();

        var updates = CreateUpdatesWithNullText();

        innerMock
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns(AsAsyncEnumerable(updates));

        var sut = CreateSut(innerMock, recentMemoryMock);

        var requestMessages = new List<ChatMessage>
        {
            CreateMessage("user", "Test")
        };

        // Act
        var receivedUpdates = new List<ChatResponseUpdate>();
        await foreach (var update in sut.GetStreamingResponseAsync(
            requestMessages, ct: TestContext.Current.CancellationToken))
        {
            receivedUpdates.Add(update);
        }

        // Assert - all 3 updates received (including null text one)
        Assert.Equal(3, receivedUpdates.Count);

        // Synthesized response should only include non-null text
        recentMemoryMock.Verify(
            r => r.PushMessageAsync(
                DefaultSessionId,
                It.Is<ChatMessage>(m => m.Text == "Hello World"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Metadata attachment

    [Fact]
    public async Task PersistIncomingMessages_MissingMetadata_AttachesMetadata()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var recentMemoryMock = CreateRecentMemoryMock();

        var response = CreateResponse("Ok");
        innerMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var sut = CreateSut(innerMock, recentMemoryMock);

        // Messages without metadata but sessionId attached via another message
        var msgWithout = CreateMessageWithoutMetadata("user", "No metadata");
        var msgWith = CreateMessage("user", "Has metadata");
        var requestMessages = new List<ChatMessage> { msgWithout, msgWith };

        // Act
        await sut.GetResponseAsync(requestMessages, ct: TestContext.Current.CancellationToken);

        // Assert - metadata should be attached to msgWithout
        var attachedMetadata = msgWithout.Contents.OfType<MemoryMetadataContent>().FirstOrDefault();
        Assert.NotNull(attachedMetadata);
        Assert.Equal(DefaultSessionId, attachedMetadata.SessionId);
        Assert.NotEmpty(attachedMetadata.MessageId);
    }

    [Fact]
    public async Task PersistIncomingMessages_ExistingMetadata_Preserved()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var recentMemoryMock = CreateRecentMemoryMock();

        var response = CreateResponse("Ok");
        innerMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var sut = CreateSut(innerMock, recentMemoryMock);

        var msg = CreateMessage("user", "Has metadata", messageId: "preset-id-123");

        // Act
        await sut.GetResponseAsync([msg], ct: TestContext.Current.CancellationToken);

        // Assert - existing metadata preserved
        var metadata = msg.Contents.OfType<MemoryMetadataContent>().First();
        Assert.Equal("preset-id-123", metadata.MessageId);
        Assert.Equal(DefaultSessionId, metadata.SessionId);
    }

    [Fact]
    public async Task PersistIncomingMessages_NoSessionIdAnywhere_SkipsAll()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var recentMemoryMock = CreateRecentMemoryMock();

        var response = CreateResponse("Ok");
        innerMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var sut = CreateSut(innerMock, recentMemoryMock);

        var messages = new List<ChatMessage>
        {
            CreateMessageWithoutMetadata("user", "No session 1"),
            CreateMessageWithoutMetadata("user", "No session 2")
        };

        // Act
        await sut.GetResponseAsync(messages, ct: TestContext.Current.CancellationToken);

        // Assert - no persistence occurs when the batch contains no sessionId
        recentMemoryMock.Verify(
            r => r.PushMessageAsync(
                It.IsAny<string>(),
                It.IsAny<ChatMessage>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        Assert.All(
            messages,
            message => Assert.DoesNotContain(
                message.Contents,
                content => content is MemoryMetadataContent));
    }

    #endregion

    #region Response persistence edge cases

    [Fact]
    public async Task PersistResponseMessages_EmptyResponse_NoPersistence()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var recentMemoryMock = CreateRecentMemoryMock();

        var response = new ChatResponse([]); // empty message list
        innerMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var sut = CreateSut(innerMock, recentMemoryMock);

        var requestMessages = new List<ChatMessage>
        {
            CreateMessage("user", "Hello")
        };

        // Act
        await sut.GetResponseAsync(requestMessages, ct: TestContext.Current.CancellationToken);

        // Assert - incoming persisted, but no response persisted (empty)
        recentMemoryMock.Verify(
            r => r.PushMessageAsync(
                DefaultSessionId,
                It.Is<ChatMessage>(m => m.Text == "Hello"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Only the incoming message was pushed (exactly 1 call total)
        recentMemoryMock.Verify(
            r => r.PushMessageAsync(
                It.IsAny<string>(),
                It.IsAny<ChatMessage>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
