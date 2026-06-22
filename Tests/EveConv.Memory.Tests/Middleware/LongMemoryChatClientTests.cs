using EveConv.Abstraction.Memory;
using EveConv.Memory.Config;
using EveConv.Memory.Middleware;
using EveConv.Memory.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace EveConv.Memory.Tests.Middleware;

/// <summary>
/// Unit tests for <see cref="LongMemoryChatClient"/> using mocked
/// <see cref="IChatClient"/> and <see cref="ILongMemory"/>.
/// Each test isolates a single path through the middleware.
/// </summary>
public class LongMemoryChatClientTests
{
    private const string DefaultSessionId = "session-unit-1";
    private const string DefaultOwnerKey = "test-owner";

    #region helpers

    private static Mock<IChatClient> CreateInnerMock()
    {
        return new Mock<IChatClient>();
    }

    private static Mock<ILongMemory> CreateLongMemoryMock()
    {
        return new Mock<ILongMemory>();
    }

    private static LongMemoryChatClient CreateSut(
        Mock<IChatClient>? innerMock = null,
        Mock<ILongMemory>? longMemoryMock = null)
    {
        innerMock ??= CreateInnerMock();
        longMemoryMock ??= CreateLongMemoryMock();

        return new LongMemoryChatClient(
            innerMock.Object,
            longMemoryMock.Object,
            NullLoggerFactory.Instance);
    }

    /// <summary>
    /// Creates a <see cref="ChatMessage"/> with <see cref="MemoryMetadataContent"/> attached.
    /// </summary>
    private static ChatMessage CreateMsg(
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
    private static ChatMessage CreateMsgWithoutMetadata(string role, string text)
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

    #endregion

    #region GetResponseAsync — long memory injection

    /// <summary>
    /// Verify that long-term memory context messages are injected before
    /// the original messages when calling the inner chat client.
    /// </summary>
    [Fact]
    public async Task GetResponseAsync_LongMemoryReturnsContext_InjectsBeforeInner()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var longMemoryMock = CreateLongMemoryMock();

        var contextMessages = new List<ChatMessage>
        {
            new(ChatRole.System, "(preference) Alice prefers C# and .NET."),
            new(ChatRole.System, "(habit) Alice goes hiking every Saturday morning.")
        };

        longMemoryMock
            .Setup(m => m.GetContextMessagesAsync(DefaultOwnerKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contextMessages.AsReadOnly());

        var response = CreateResponse("Hello Alice!");

        List<ChatMessage>? capturedMessages = null;
        innerMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<ChatMessage> msgs, ChatOptions? _, CancellationToken _) =>
                capturedMessages = msgs.ToList())
            .ReturnsAsync(response);

        var sut = CreateSut(innerMock, longMemoryMock);

        var requestMessages = new List<ChatMessage>
        {
            CreateMsg("user", "What's my favorite tech stack?")
        };

        // Act
        var result = await sut.GetResponseAsync(requestMessages, ct: TestContext.Current.CancellationToken);

        // Assert — response forwarded
        Assert.Equal(response, result);

        // Verify augmented messages: 2 long memory + 1 user
        Assert.NotNull(capturedMessages);
        Assert.Equal(3, capturedMessages.Count);
        Assert.Equal("(preference) Alice prefers C# and .NET.", capturedMessages[0].Text);
        Assert.Equal("(habit) Alice goes hiking every Saturday morning.", capturedMessages[1].Text);
        Assert.Equal("What's my favorite tech stack?", capturedMessages[2].Text);
    }

    /// <summary>
    /// When ILongMemory returns an empty list, the original messages
    /// are passed to the inner client unchanged.
    /// </summary>
    [Fact]
    public async Task GetResponseAsync_NoLongMemory_PassesMessagesDirectly()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var longMemoryMock = CreateLongMemoryMock();

        longMemoryMock
            .Setup(m => m.GetContextMessagesAsync(DefaultOwnerKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = CreateResponse("Hi there!");

        List<ChatMessage>? capturedMessages = null;
        innerMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<ChatMessage> msgs, ChatOptions? _, CancellationToken _) =>
                capturedMessages = msgs.ToList())
            .ReturnsAsync(response);

        var sut = CreateSut(innerMock, longMemoryMock);

        var requestMessages = new List<ChatMessage>
        {
            CreateMsg("user", "Hello")
        };

        // Act
        var result = await sut.GetResponseAsync(requestMessages, ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(response, result);
        Assert.NotNull(capturedMessages);
        Assert.Single(capturedMessages);
        Assert.Equal("Hello", capturedMessages[0].Text);
    }

    /// <summary>
    /// When ILongMemory returns null, the original messages
    /// are passed to the inner client unchanged.
    /// </summary>
    [Fact]
    public async Task GetResponseAsync_LongMemoryReturnsNull_PassesMessagesDirectly()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var longMemoryMock = CreateLongMemoryMock();

        longMemoryMock
            .Setup(m => m.GetContextMessagesAsync(DefaultOwnerKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ChatMessage>)null!);

        var response = CreateResponse("Hi there!");

        List<ChatMessage>? capturedMessages = null;
        innerMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<ChatMessage> msgs, ChatOptions? _, CancellationToken _) =>
                capturedMessages = msgs.ToList())
            .ReturnsAsync(response);

        var sut = CreateSut(innerMock, longMemoryMock);

        var requestMessages = new List<ChatMessage>
        {
            CreateMsg("user", "Hello")
        };

        // Act
        var result = await sut.GetResponseAsync(requestMessages, ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(response, result);
        Assert.NotNull(capturedMessages);
        Assert.Single(capturedMessages);
        Assert.Equal("Hello", capturedMessages[0].Text);
    }

    /// <summary>
    /// Null messages should be handled gracefully without throwing.
    /// </summary>
    [Fact]
    public async Task GetResponseAsync_NullMessages_HandlesGracefully()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var longMemoryMock = CreateLongMemoryMock();

        longMemoryMock
            .Setup(m => m.GetContextMessagesAsync(DefaultOwnerKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = CreateResponse("Response to null");
        innerMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var sut = CreateSut(innerMock, longMemoryMock);

        // Act — should not throw
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

    /// <summary>
    /// When ILongMemory.GetContextMessagesAsync throws, the middleware should
    /// fall back to original messages and still invoke the inner client.
    /// </summary>
    [Fact]
    public async Task GetResponseAsync_AugmentationError_FallsBackToOriginal()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var longMemoryMock = CreateLongMemoryMock();

        longMemoryMock
            .Setup(m => m.GetContextMessagesAsync(DefaultOwnerKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection lost"));

        var response = CreateResponse("Fallback response");

        List<ChatMessage>? capturedMessages = null;
        innerMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<ChatMessage> msgs, ChatOptions? _, CancellationToken _) =>
                capturedMessages = msgs.ToList())
            .ReturnsAsync(response);

        var sut = CreateSut(innerMock, longMemoryMock);

        var requestMessages = new List<ChatMessage>
        {
            CreateMsg("user", "Hello despite error")
        };

        // Act — should not throw
        var result = await sut.GetResponseAsync(requestMessages, ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(response, result);
        // Inner was called with original messages (no augmentation)
        Assert.NotNull(capturedMessages);
        Assert.Single(capturedMessages);
        Assert.Equal("Hello despite error", capturedMessages[0].Text);
    }

    #endregion

    #region GetStreamingResponseAsync — long memory injection

    /// <summary>
    /// Streaming path should also inject long-term memory context
    /// before the original messages.
    /// </summary>
    [Fact]
    public async Task GetStreamingResponseAsync_WithLongMemory_InjectsContext()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var longMemoryMock = CreateLongMemoryMock();

        var contextMessages = new List<ChatMessage>
        {
            new(ChatRole.System, "(fact) Alice works at Contoso.")
        };

        longMemoryMock
            .Setup(m => m.GetContextMessagesAsync(DefaultOwnerKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contextMessages.AsReadOnly());

        var chunks = new[] { "Hello", " ", "Alice" };

        List<ChatMessage>? capturedMessages = null;
        innerMock
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<ChatMessage> msgs, ChatOptions? _, CancellationToken _) =>
                capturedMessages = msgs.ToList())
            .Returns(CreateStreamingUpdates(chunks));

        var sut = CreateSut(innerMock, longMemoryMock);

        var requestMessages = new List<ChatMessage>
        {
            CreateMsg("user", "Hi")
        };

        // Act
        var receivedUpdates = new List<ChatResponseUpdate>();
        await foreach (var update in sut.GetStreamingResponseAsync(
                           requestMessages, ct: TestContext.Current.CancellationToken))
        {
            receivedUpdates.Add(update);
        }

        // Assert — all chunks received
        Assert.Equal(3, receivedUpdates.Count);

        // Verify augmented messages: 1 long memory + 1 user
        Assert.NotNull(capturedMessages);
        Assert.Equal(2, capturedMessages.Count);
        Assert.Equal("(fact) Alice works at Contoso.", capturedMessages[0].Text);
        Assert.Equal("Hi", capturedMessages[1].Text);
    }

    /// <summary>
    /// Streaming path with no long memory should pass messages directly.
    /// </summary>
    [Fact]
    public async Task GetStreamingResponseAsync_NoLongMemory_PassesMessagesDirectly()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var longMemoryMock = CreateLongMemoryMock();

        longMemoryMock
            .Setup(m => m.GetContextMessagesAsync(DefaultOwnerKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var chunks = new[] { "Hi" };

        List<ChatMessage>? capturedMessages = null;
        innerMock
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<ChatMessage> msgs, ChatOptions? _, CancellationToken _) =>
                capturedMessages = msgs.ToList())
            .Returns(CreateStreamingUpdates(chunks));

        var sut = CreateSut(innerMock, longMemoryMock);

        var requestMessages = new List<ChatMessage>
        {
            CreateMsg("user", "Yo")
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
        Assert.NotNull(capturedMessages);
        Assert.Single(capturedMessages);
        Assert.Equal("Yo", capturedMessages[0].Text);
    }

    #endregion

    #region Background extraction — debounce

    /// <summary>
    /// Extraction should be triggered on every 10th request (debounce logic).
    /// The first 9 requests skip extraction; the 10th fires it.
    /// </summary>
    [Fact]
    public async Task ExtractInBackground_OnTenthRequest_TriggersExtraction()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var longMemoryMock = CreateLongMemoryMock();

        longMemoryMock
            .Setup(m => m.GetContextMessagesAsync(DefaultOwnerKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = CreateResponse("Ok");
        innerMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var sut = CreateSut(innerMock, longMemoryMock);

        // Act — send 9 requests (should NOT trigger extraction)
        for (int i = 1; i <= 9; i++)
        {
            var msg = CreateMsg("user", $"Request {i}", sessionId: DefaultSessionId);
            await sut.GetResponseAsync([msg], ct: TestContext.Current.CancellationToken);
        }

        // Assert — extraction NOT called yet
        longMemoryMock.Verify(
            m => m.ExtractAndStoreAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        // Act — 10th request SHOULD trigger extraction
        var msg10 = CreateMsg("user", "Request 10", sessionId: DefaultSessionId);
        await sut.GetResponseAsync([msg10], ct: TestContext.Current.CancellationToken);

        // Assert — extraction was triggered
        // Note: ExtractInBackground is fire-and-forget, so we allow a short wait
        await Task.Delay(200, TestContext.Current.CancellationToken);

        longMemoryMock.Verify(
            m => m.ExtractAndStoreAsync(
                DefaultOwnerKey,
                It.Is<IEnumerable<string>>(ids => ids.Contains(DefaultSessionId)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Messages without <see cref="MemoryMetadataContent"/> should not
    /// trigger background extraction.
    /// </summary>
    [Fact]
    public async Task ExtractInBackground_NoMetadata_SkipsExtraction()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var longMemoryMock = CreateLongMemoryMock();

        longMemoryMock
            .Setup(m => m.GetContextMessagesAsync(DefaultOwnerKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = CreateResponse("Ok");
        innerMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var sut = CreateSut(innerMock, longMemoryMock);

        // Act — send 10 requests WITHOUT metadata
        for (int i = 1; i <= 10; i++)
        {
            var msg = CreateMsgWithoutMetadata("user", $"Request {i}");
            await sut.GetResponseAsync([msg], ct: TestContext.Current.CancellationToken);
        }

        // Allow background task to complete
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Assert — extraction was NEVER called (no session IDs to extract from)
        longMemoryMock.Verify(
            m => m.ExtractAndStoreAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// When <see cref="ILongMemory.ExtractAndStoreAsync"/> throws,
    /// the error should be logged and not propagate to the caller.
    /// </summary>
    [Fact]
    public async Task ExtractInBackground_ExtractionError_DoesNotThrowToCaller()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var longMemoryMock = CreateLongMemoryMock();

        longMemoryMock
            .Setup(m => m.GetContextMessagesAsync(DefaultOwnerKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        longMemoryMock
            .Setup(m => m.ExtractAndStoreAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Extraction failed"));

        var response = CreateResponse("Ok");
        innerMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var sut = CreateSut(innerMock, longMemoryMock);

        // Act — send 10 requests; the 10th triggers background extraction which will fail
        for (int i = 1; i <= 10; i++)
        {
            var msg = CreateMsg("user", $"Request {i}", sessionId: DefaultSessionId);
            // Should NOT throw despite background extraction failure
            var result = await sut.GetResponseAsync([msg], ct: TestContext.Current.CancellationToken);
            Assert.Equal("Ok", result.Messages[0].Text);
        }
    }

    #endregion

    #region GetResponseAsync — pass-through of options and cancellation token

    /// <summary>
    /// ChatOptions and CancellationToken should be forwarded to the inner client.
    /// </summary>
    [Fact]
    public async Task GetResponseAsync_PassesOptionsAndCancellationToken()
    {
        // Arrange
        var innerMock = CreateInnerMock();
        var longMemoryMock = CreateLongMemoryMock();

        longMemoryMock
            .Setup(m => m.GetContextMessagesAsync(DefaultOwnerKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = CreateResponse("Ok");
        var chatOptions = new ChatOptions { Temperature = 0.7f };

        innerMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var sut = CreateSut(innerMock, longMemoryMock);

        var requestMessages = new List<ChatMessage>
        {
            CreateMsg("user", "Hi")
        };

        // Act
        await sut.GetResponseAsync(requestMessages, chatOptions, TestContext.Current.CancellationToken);

        // Assert
        innerMock.Verify(
            c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                chatOptions,
                TestContext.Current.CancellationToken),
            Times.Once);
    }

    #endregion
}
