using EveConv.Abstraction.DocParser;

namespace EveConv.Plugins.Api;

/// <summary>
/// Defines a plugin that parses a custom document format.
/// </summary>
public interface IDocumentParserPlugin : IPlugin, IDocumentParser
{
}
