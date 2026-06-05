using EveConv.Abstraction.Memory;
using EveConv.Memory.Models;
using EveConv.Memory.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EveConv.Memory.Tests;

/// <summary>
/// Unit tests for the TiktokenCounter service.
/// </summary>
public class TiktokenCounterTests
{
    [Fact]
    public void CountTokens_EmptyString_ReturnsZero()
    {
        var counter = new TiktokenCounter();
        var result = counter.CountTokens("");
        Assert.Equal(0, result);
    }

    [Fact]
    public void CountTokens_NormalText_ReturnsEstimatedTokens()
    {
        var counter = new TiktokenCounter();
        // 20 chars / 4 = 5 tokens
        var result = counter.CountTokens("Hello world test msg");
        Assert.Equal(5, result);
    }

    [Fact]
    public void CountTokens_NullString_ReturnsZero()
    {
        var counter = new TiktokenCounter();
        var result = counter.CountTokens((string)null!);
        Assert.Equal(0, result);
    }

    [Fact]
    public void CountTokens_EmptyMessageList_ReturnsZero()
    {
        var counter = new TiktokenCounter();
        var result = counter.CountTokens([]);
        Assert.Equal(0, result);
    }

    [Fact]
    public void CountTokens_MessagesWithTextContent_ReturnsEstimatedTokens()
    {
        var counter = new TiktokenCounter();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello, how are you?"),
            new(ChatRole.Assistant, "I'm doing well, thank you!")
        };

        var result = counter.CountTokens(messages);
        // "Hello, how are you?" = 19 chars → 5 tokens
        // "I'm doing well, thank you!" = 27 chars → 7 tokens
        // 4 overhead per message × 2 = 8
        // Total ≈ 20
        Assert.True(result > 0);
        Assert.True(result < 50);
    }
}

/// <summary>
/// Unit tests for MemoryMetadataContent JSON serialization round-trip.
/// </summary>
public class MemoryMetadataContentSerializationTests
{
    [Fact]
    public void RoundTrip_ThroughChatMessage_PreservesMetadata()
    {
        // Configure JSON serialization with AIJsonUtilities
        var jsonOptions = new System.Text.Json.JsonSerializerOptions();
        AIJsonUtilities.AddAIContentType(
            jsonOptions, typeof(MemoryMetadataContent), "eveconv_memory_metadata");

        // Create a ChatMessage with metadata
        var message = new ChatMessage(ChatRole.User, "Test message");
        var metadata = new MemoryMetadataContent
        {
            MessageId = "msg-001",
            SessionId = "session-abc"
        };
        message.Contents.Add(metadata);

        // Serialize to JSON using MEAI-aware options
        var json = System.Text.Json.JsonSerializer.Serialize(message, jsonOptions);

        // Deserialize back
        var restored = System.Text.Json.JsonSerializer.Deserialize<ChatMessage>(json, jsonOptions);
        Assert.NotNull(restored);

        // Verify metadata survived
        var restoredMeta = restored!.Contents.OfType<MemoryMetadataContent>().FirstOrDefault();
        Assert.NotNull(restoredMeta);
        Assert.Equal("msg-001", restoredMeta!.MessageId);
        Assert.Equal("session-abc", restoredMeta.SessionId);
    }

    [Fact]
    public void RoundTrip_WithoutRegistration_FailsGracefully()
    {
        // Without AIJsonUtilities registration, serialization should fail
        var message = new ChatMessage(ChatRole.User, "Test message");
        message.Contents.Add(new MemoryMetadataContent
        {
            MessageId = "msg-001",
            SessionId = "session-abc"
        });

        Assert.Throws<System.NotSupportedException>(() =>
        {
            System.Text.Json.JsonSerializer.Serialize(message);
        });
    }
}

/// <summary>
/// Unit tests for InMemorySessionMemoryStore.
/// </summary>
public class InMemorySessionMemoryStoreTests
{
    [Fact]
    public async Task SaveAndGetMessages_RoundTrip_PreservesData()
    {
        var store = new EveConv.Memory.Stores.InMemorySessionMemoryStore();
        var message = new ChatMessage(ChatRole.User, "Hello");
        message.Contents.Add(new MemoryMetadataContent
        {
            MessageId = "msg-1",
            SessionId = "session-1"
        });

        await store.SaveMessagesAsync("session-1", [message]);

        var retrieved = await store.GetMessagesAsync("session-1");
        Assert.Single(retrieved);
        Assert.Equal(ChatRole.User, retrieved[0].Role);
    }

    [Fact]
    public async Task GetSession_NonExistent_ReturnsNull()
    {
        var store = new EveConv.Memory.Stores.InMemorySessionMemoryStore();
        var result = await store.GetSessionAsync("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAndGetSession_RoundTrip_PreservesData()
    {
        var store = new EveConv.Memory.Stores.InMemorySessionMemoryStore();
        var session = new ChatSession
        {
            SessionId = "session-1",
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow,
            TotalMessageCount = 5
        };

        await store.SaveSessionAsync(session);
        var retrieved = await store.GetSessionAsync("session-1");

        Assert.NotNull(retrieved);
        Assert.Equal("session-1", retrieved!.SessionId);
        Assert.Equal(5, retrieved.TotalMessageCount);
    }

    [Fact]
    public async Task SaveAndGetCompactions_RoundTrip_PreservesData()
    {
        var store = new EveConv.Memory.Stores.InMemorySessionMemoryStore();
        var compaction = new SessionCompaction
        {
            Id = "comp-1",
            SessionId = "session-1",
            CompactedSummary = "Summary of messages",
            SourceMessageIds = ["msg-1", "msg-2"],
            OriginalTokenCount = 100,
            CompactedTokenCount = 20,
            CompactionLevel = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await store.SaveCompactionAsync(compaction);
        var retrieved = await store.GetCompactionsAsync("session-1");

        Assert.Single(retrieved);
        Assert.Equal("Summary of messages", retrieved[0].CompactedSummary);
        Assert.Equal(2, retrieved[0].SourceMessageIds.Count);
    }
}

/// <summary>
/// Unit tests for EveConvChatReducer compaction decision logic.
/// </summary>
public class EveConvChatReducerDecisionTests
{
    [Fact]
    public void TokenCounter_WithinWindow_NoCompactionNeeded()
    {
        var counter = new TiktokenCounter();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hi"),
            new(ChatRole.Assistant, "Hello!")
        };
        // Very short messages, well under 4096

        var tokens = counter.CountTokens(messages);
        Assert.True(tokens < 4096);
    }

    [Fact]
    public void TokenCounter_LongConversation_ExceedsWindow()
    {
        var counter = new TiktokenCounter();
        var longText = new string('x', 20000); // 20000 chars ≈ 5000 tokens
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, longText)
        };

        var tokens = counter.CountTokens(messages);
        Assert.True(tokens > 4096);
    }
}
