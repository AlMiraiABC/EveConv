using EveConv.Abstraction.Cache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EveConv.Cache.RocksDb;

/// <summary>
/// Extension methods for registering <see cref="RocksDbCache"/> services with dependency injection.
/// </summary>
public static class DependencyInjectExtensions
{
    /// <summary>
    /// Adds <see cref="RocksDbCache"/> services to the service collection with default configuration.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRocksDbCache(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register RocksDbCache as singleton for all cache interfaces
        services.AddSingleton<RocksDbCache>();

        // Register cache interfaces
        services.AddSingleton(typeof(IBasicCache<object>), provider => provider.GetRequiredService<RocksDbCache>());
        services.AddSingleton(typeof(IBatchCache<object>), provider => provider.GetRequiredService<RocksDbCache>());
        services.AddSingleton(typeof(IEnhanceCache<object>), provider => provider.GetRequiredService<RocksDbCache>());
        services.AddSingleton(typeof(IListCache<object>), provider => provider.GetRequiredService<RocksDbCache>());

        return services;
    }

    /// <summary>
    /// Adds <see cref="RocksDbCache"/> services to the service collection with configuration action.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureOptions">Action to configure <see cref="RocksDbConfiguration"/> options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRocksDbCache(this IServiceCollection services, Action<RocksDbConfiguration> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        // Configure RocksDbConfiguration
        services.Configure(configureOptions);

        services.AddRocksDbCache();

        return services;
    }

    /// <summary>
    /// Adds <see cref="RocksDbCache"/> services to the service collection with configuration section.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configurationSection">The configuration section instance to bind from.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRocksDbCache(this IServiceCollection services, IConfigurationSection configurationSection)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configurationSection);

        // Bind configuration from appsettings
        services.Configure<RocksDbConfiguration>(configurationSection);

        services.AddRocksDbCache();

        return services;
    }
}
