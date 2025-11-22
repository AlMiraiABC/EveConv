using System;
using EveConv.Abstraction.Cache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EveConv.Cache.InMemory;

/// <summary>
/// Extension methods for registering <see cref="InMemoryCache"/> services with dependency injection.
/// </summary>
public static class DependencyInjectExtensions
{
    /// <summary>
    /// Adds <see cref="InMemoryCache"/> services to the service collection with default configuration.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInMemoryCache(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register in-memory cache as singleton for all cache interfaces
        services.AddSingleton<InMemoryCache>();

        // Register cache interfaces
        services.AddSingleton(typeof(IBasicCache<object>), provider => provider.GetRequiredService<InMemoryCache>());
        services.AddSingleton(typeof(IBatchCache<object>), provider => provider.GetRequiredService<InMemoryCache>());
        services.AddSingleton(typeof(IEnhanceCache<object>), provider => provider.GetRequiredService<InMemoryCache>());
        return services;
    }

    /// <summary>
    /// Adds <see cref="InMemoryCache"/> services to the service collection with configuration action.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureOptions">Action to configure <see cref="InMemoryConfiguration"/> options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInMemoryCache(this IServiceCollection services, Action<InMemoryConfiguration> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        // Configure InMemoryConfiguration
        services.Configure(configureOptions);

        services.AddInMemoryCache();

        return services;
    }

    /// <summary>
    /// Adds <see cref="InMemoryCache"/> services to the service collection with configuration section.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configurationSection">The configuration section instance to bind from.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInMemoryCache(this IServiceCollection services, IConfigurationSection configurationSection)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configurationSection);

        // Bind configuration from appsettings
        services.Configure<InMemoryConfiguration>(configurationSection);

        services.AddInMemoryCache();

        return services;
    }
}