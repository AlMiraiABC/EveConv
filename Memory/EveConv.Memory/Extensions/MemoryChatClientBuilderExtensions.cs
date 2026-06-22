using EveConv.Abstraction.Memory;
using EveConv.Memory.Middleware;
using EveConv.Memory.Config;
using EveConv.Memory.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EveConv.Memory.Extensions;

/// <summary>
/// Extension methods for integrating the EveConv memory system into the MEAI chat pipeline.
/// </summary>
public static class MemoryChatClientBuilderExtensions
{
    /// <summary>
    /// Adds EveConv memory middleware to the chat pipeline using externally provided
    /// instances, without requiring an <see cref="IServiceProvider"/>.
    /// <list type="number">
    /// <item><description>Session compaction via <see cref="EveConvChatReducer"/></description></item>
    /// <item><description>Automatic message persistence via <see cref="MemoryPersistingChatClient"/></description></item>
    /// <item><description>Long-term memory injection via <see cref="LongMemoryChatClient"/></description></item>
    /// </list>
    /// </summary>
    /// <param name="builder">The <see cref="ChatClientBuilder"/> to configure.</param>
    /// <param name="reducer">The compaction reducer to use.</param>
    /// <param name="recentMemory">The recent memory service for message persistence.</param>
    /// <param name="longMemory">The long-term memory service for context injection.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>The builder for chaining.</returns>
    public static ChatClientBuilder UseMemory(
        this ChatClientBuilder builder,
        EveConvChatReducer reducer,
        IRecentMemory recentMemory,
        ILongMemory longMemory,
        ILoggerFactory? loggerFactory = null)
    {
        return builder
            .UseChatReducer(reducer)
            .Use((inner, _) => new MemoryPersistingChatClient(inner, recentMemory, loggerFactory))
            .Use((inner, _) => new LongMemoryChatClient(inner, longMemory, loggerFactory));
    }

    /// <summary>
    /// Adds EveConv memory middleware to the chat pipeline, resolving or creating an
    /// <see cref="EveConvChatReducer"/> from the provided <see cref="IServiceProvider"/>.
    /// </summary>
    /// <param name="builder">The <see cref="ChatClientBuilder"/> to configure.</param>
    /// <param name="services">The <see cref="IServiceProvider"/> to resolve dependencies.</param>
    /// <returns>The builder for chaining.</returns>
    public static ChatClientBuilder UseMemory(this ChatClientBuilder builder, IServiceProvider services)
    {
        var reducer = services.GetService<EveConvChatReducer>()
            ?? new EveConvChatReducer(
                services.GetRequiredKeyedService<IChatClient>("summarization"),
                services.GetRequiredService<ISessionMemory>(),
                services.GetRequiredService<ITokenCounter>(),
                services.GetRequiredService<IOptions<MemoryConfiguration>>(),
                services.GetService<ILoggerFactory>());

        return builder.UseMemory(
            reducer,
            services.GetRequiredService<IRecentMemory>(),
            services.GetRequiredService<ILongMemory>(),
            services.GetService<ILoggerFactory>());
    }
}
