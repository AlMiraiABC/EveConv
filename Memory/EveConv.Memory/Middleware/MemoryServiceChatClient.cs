using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.Memory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace EveConv.Memory.Middleware;

public sealed class MemoryServiceChatClient : DelegatingChatClient
{
    private readonly IMemoryService _memoryService;
    private readonly ILogger _logger;

    public MemoryServiceChatClient(IChatClient inner, IMemoryService memoryService,
        ILoggerFactory? loggerFactory = null) : base(inner)
    {
        _memoryService = memoryService;
        _logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<MemoryServiceChatClient>();
    }

    public Func<string>? GetSessionId { get; set; }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
    {
        var sessionId = GetSessionId?.Invoke() ?? "default";
        var messageList = messages?.ToList() ?? [];

        // BEFORE: 自动持久化传入消息
        foreach (var msg in messageList)
        {
            await _memoryService.AddMessageAsync(sessionId, msg, ct);
        }

        // BEFORE: 获取完整上下文（已压缩的 session history + 长期记忆）
        var context = await _memoryService.GetContextAsync(sessionId, ct);
        var augmentedMessages = new List<ChatMessage>(
            context.LongMemoryMessages.Count + context.ModelMessages.Count);
        augmentedMessages.AddRange(context.LongMemoryMessages);
        augmentedMessages.AddRange(context.ModelMessages);

        // 调用内部客户端（自动触发 IChatReducer 压缩）
        var response = await base.GetResponseAsync(augmentedMessages, options, ct);

        // AFTER: 持久化响应
        foreach (var msg in response.Messages)
        {
            await _memoryService.AddMessageAsync(sessionId, msg, ct);
        }

        return response;
    }
}
