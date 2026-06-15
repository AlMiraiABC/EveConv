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
    /// Adds EveConv memory middleware to the chat pipeline:
    /// <list type="number">
    /// <item><description>Session compaction via <see cref="EveConvChatReducer"/></description></item>
    /// <item><description>Automatic message persistence via <see cref="MemoryPersistingChatClient"/></description></item>
    /// <item><description>Long-term memory injection via <see cref="LongMemoryChatClient"/></description></item>
    /// </list>
    /// </summary>
    /// <param name="builder">The <see cref="ChatClientBuilder"/> to configure.</param>
    /// <param name="services">The <see cref="IServiceProvider"/> to resolve dependencies.</param>
    /// <returns>The builder for chaining.</returns>
    public static ChatClientBuilder UseMemory(this ChatClientBuilder builder, IServiceProvider services)
    {
        var reducer = services.GetRequiredService<EveConvChatReducer>();

        return builder
            .UseChatReducer(reducer)
            .Use((inner, sp) =>
            {
                var recentMemory = sp.GetRequiredService<IRecentMemory>();
                var loggerFactory = sp.GetService<ILoggerFactory>();
                return new MemoryPersistingChatClient(inner, recentMemory, loggerFactory);
            })
            .Use((inner, sp) =>
            {
                var longMemory = sp.GetRequiredService<ILongMemory>();
                var options = sp.GetRequiredService<IOptions<LongMemoryOptions>>();
                var loggerFactory = sp.GetService<ILoggerFactory>();
                return new LongMemoryChatClient(inner, longMemory, options, loggerFactory);
            });
    }
}
