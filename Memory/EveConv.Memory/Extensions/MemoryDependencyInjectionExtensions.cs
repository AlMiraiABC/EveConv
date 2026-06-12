using System.Text.Json;
using EveConv.Abstraction.Cache;
using EveConv.Abstraction.Memory;
using EveConv.Memory.Managers;
using EveConv.Memory.Models;
using EveConv.Memory.Options;
using EveConv.Memory.Services;
using EveConv.Memory.Stores;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace EveConv.Memory.Extensions;

/// <summary>
/// Extension methods for registering EveConv memory services in the DI container.
/// </summary>
public static class MemoryDependencyInjectionExtensions
{
    /// <summary>
    /// Registers all EveConv memory services.
    /// Requires <see cref="ICache"/> (IBasicCache + IListCache + IBatchCache + IEnhanceCache)
    /// to be registered separately by the cache implementation project.
    /// Requires keyed <c>IChatClient</c> singletons with keys <c>"summarization"</c> and
    /// <c>"extraction"</c> to be registered separately by the startup project.
    /// Optionally requires <see cref="ISqlSugarClient"/> for RDB persistence.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEveConvMemory(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MemoryOptions>(
            configuration.GetSection("Memory"));
        services.Configure<LongMemoryOptions>(
            configuration.GetSection("Memory:LongMemory"));

        // --- Token counter ---
        services.AddSingleton<ITokenCounter, TiktokenCounter>();

        // --- Store: auto-detect RDB or fallback to in-memory ---
        services.AddSingleton<ISessionMemory>(sp =>
        {
            var sqlClient = sp.GetService<ISqlSugarClient>();
            if (sqlClient is not null)
            {
                var logger = sp.GetService<ILoggerFactory>();
                return new RdbSessionMemory(sqlClient, logger);
            }
            return new InMemorySessionMemory();
        });

        // --- Core services ---
        services.AddSingleton<IRecentMemory, RecentMemoryManager>();
        services.AddSingleton<IMemoryService, MemoryService>();
        services.AddSingleton<EveConvChatReducer>();
        services.AddSingleton<LLMLongMemoryExtractor>();
        services.AddSingleton<ILongMemory, LongMemoryManager>();

        return services;
    }

    /// <summary>
    /// Registers the <see cref="MemoryMetadataContent"/> type with MEAI's JSON serialization
    /// so it round-trips correctly through <see cref="ChatMessage"/> serialization.
    /// </summary>
    /// <param name="options">The <see cref="System.Text.Json.JsonSerializerOptions"/> to configure.</param>
    public static void AddMemoryJsonSerialization(this JsonSerializerOptions options)
    {
        AIJsonUtilities.AddAIContentType(
            options, typeof(MemoryMetadataContent), "eveconv_memory_metadata");
    }
}
