using System;
using System.Collections.Generic;
using System.Text;
using EveConv.Abstraction.VectorStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EveConv.VectorStorage.Qdrant
{
    /// <summary>
    /// Extension methods for registering <see cref="QdrantVectorStorage"/> services with dependency injection.
    /// </summary>
    public static class DependencyInjection
    {
        /// <summary>
        /// Adds <see cref="QdrantVectorStorage"/> services to the service collection with default configuration.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddQdrantVectorStorage(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);
            services.AddSingleton<IVectorStorage, QdrantVectorStorage>();
            return services;
        }

        /// <summary>
        /// Adds <see cref="QdrantVectorStorage"/> services to the service collection with configuration action.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="configureOptions">Action to configure <see cref="QdrantConfiguration"/> options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddQdrantVectorStorage(this IServiceCollection services, Action<QdrantConfiguration> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(services);
            services.Configure(configureOptions);
            services.AddQdrantVectorStorage();
            return services;
        }

        /// <summary>
        /// Adds <see cref="QdrantVectorStorage"/> services to the service collection with configuration section.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="configurationSection">The configuration section instance to bind from.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddQdrantVectorStorage(this IServiceCollection services, IConfigurationSection configurationSection)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configurationSection);
            services.Configure<QdrantConfiguration>(configurationSection);
            services.AddQdrantVectorStorage();
            return services;
        }
    }
}
