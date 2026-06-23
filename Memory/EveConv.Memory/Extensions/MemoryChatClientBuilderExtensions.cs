using EveConv.Abstraction.Memory;
using EveConv.Memory.Middleware;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    /// <item><description>Automatic message persistence via <see cref="MemoryServiceChatClient"/></description></item>
    /// <item><description>Session compaction and long-term memory injection via <see cref="IMemoryService.GetContextAsync"/></description></item>
    /// </list>
    /// </summary>
    /// <param name="builder">The <see cref="ChatClientBuilder"/> to configure.</param>
    /// <param name="memoryService">The <see cref="IMemoryService"/> for message persistence, compaction, and context retrieval.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>The builder for chaining.</returns>
    public static ChatClientBuilder UseMemory(
        this ChatClientBuilder builder,
        IMemoryService memoryService,
        ILoggerFactory? loggerFactory = null)
    {
        return builder
            .Use((inner, _) => new MemoryServiceChatClient(inner, memoryService, loggerFactory));
    }

    /// <summary>
    /// Adds EveConv memory middleware to the chat pipeline, resolving dependencies
    /// from the provided <see cref="IServiceProvider"/>.
    /// </summary>
    /// <param name="builder">The <see cref="ChatClientBuilder"/> to configure.</param>
    /// <param name="services">The <see cref="IServiceProvider"/> to resolve dependencies.</param>
    /// <returns>The builder for chaining.</returns>
    public static ChatClientBuilder UseMemory(this ChatClientBuilder builder, IServiceProvider services)
    {
        return builder.UseMemory(
            services.GetRequiredService<IMemoryService>(),
            services.GetService<ILoggerFactory>());
    }
}
