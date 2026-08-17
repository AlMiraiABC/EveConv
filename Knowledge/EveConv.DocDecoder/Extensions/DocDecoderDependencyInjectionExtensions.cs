using EveConv.Abstraction;
using EveConv.Abstraction.DocParser;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EveConv.DocDecoder.Extensions;

public static class DocDecoderDependencyInjectionExtensions
{
    /// <summary>
    /// Register a <see cref="DocumentParser"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDocumentParser(
        this IServiceCollection services)
    {
        services.AddSingleton<DocumentParser>();
        return services;
    }

    /// <summary>
    /// Register a <see cref="DocumentParser"/> with specified <paramref name="parsers"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="parsers">Collection of parser which need register.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDocumentParser(
        this IServiceCollection services,
        IEnumerable<IDocumentParser> parsers)
    {
        services.AddSingleton<DocumentParser>(sp =>
        {
            var mimeTypeDetection = sp.GetRequiredService<IMimeTypeDetection>();
            var loggerFactory = sp.GetService<ILoggerFactory>();
            return new DocumentParser(parsers, mimeTypeDetection, loggerFactory);
        });
        return services;
    }
}
