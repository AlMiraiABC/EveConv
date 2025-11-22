using EveConv.Abstraction.Cache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EveConv.Cache.Redis;

/// <summary>
/// Extension methods for configuring Redis cache services in dependency injection container.
/// </summary>
public static class DependencyInjectExtensions
{
    /// <summary>
    /// Adds <see cref="RedisCache"/> services to the specified service collection with configuration options.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when services or configurationSection is null.</exception>
    public static IServiceCollection AddRedisCache(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register Redis cache as singleton for all cache interfaces
        services.AddSingleton<RedisCache>();

        // Register interface implementations that delegate to the same RedisCache instance
        services.AddSingleton<IBasicCache<object>>(provider => provider.GetRequiredService<RedisCache>());
        services.AddSingleton<IBatchCache<object>>(provider => provider.GetRequiredService<RedisCache>());
        services.AddSingleton<IEnhanceCache<object>>(provider => provider.GetRequiredService<RedisCache>());

        return services;
    }

    /// <summary>
    /// Adds <see cref="RedisCache"/> services to the specified service collection with a configuration section.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configurationSection">The configuration section containing Redis settings.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when services or configurationSection is null.</exception>
    public static IServiceCollection AddRedisCache(this IServiceCollection services, IConfigurationSection configurationSection)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configurationSection);

        // Configure Redis options
        services.Configure<RedisConfiguration>(configurationSection);

        services.AddRedisCache();
        return services;
    }

    /// <summary>
    /// Adds <see cref="RedisCache"/> services to the specified service collection with a configuration action.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureOptions">An action to configure <see cref="RedisConfiguration"/> options.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when services or configureOptions is null.</exception>
    public static IServiceCollection AddRedisCache(this IServiceCollection services, Action<RedisConfiguration> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        // Configure Redis options using the provided action
        services.Configure(configureOptions);

        services.AddRedisCache();

        return services;
    }
}