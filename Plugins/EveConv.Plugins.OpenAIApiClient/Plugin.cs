using System.ClientModel;
using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.Memory;
using EveConv.Memory.Config;
using EveConv.Memory.Extensions;
using EveConv.Memory.Managers;
using EveConv.Memory.Services;
using EveConv.Plugins.Api;
using EveConv.Plugins.Api.Abstraction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace EveConv.Plugins.OpenAIApiClient;

public class Plugin : Plugable, IDisposable
{
    private readonly ILogger<Plugin> _logger;
    private readonly IInternal _internalExport;
    private bool _disposed = false;

    private readonly Config _config;
    private readonly IChatClient _chatClient;

    protected Plugin(PluginContext context, IInternal internalExport, ILoggerFactory? loggerFactory = null)
        : base(context, loggerFactory)
    {
        this._logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<Plugin>();
        this._internalExport = internalExport;
        this._config = context.Config.Deserialize<Config>();
        var openAIChatClient = new OpenAI.Chat.ChatClient(
                this._config.ModelName,
                new ApiKeyCredential(_config.ApiKey),
                new()
                {
                    Endpoint = new Uri(this._config.Endpoint),
                })
            .AsIChatClient();
        var memoryService = _internalExport.GetService<IMemoryService>();
        if (memoryService is null)
        {
            memoryService = CreateMemoryService();
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Created new MemoryService instance for Plugin");
            }
        }
        this._chatClient = new ChatClientBuilder(openAIChatClient)
            .UseMemory(memoryService, loggerFactory)
            .Build();
    }

    private IMemoryService CreateMemoryService()
    {
        var summarizationClient = _internalExport.GetKeyedService<IChatClient>("summarization")
                                    ?? (string.IsNullOrWhiteSpace(this._config.Summerization?.ApiKey)
                                        ? CreateChatClient(this._config)
                                        : CreateChatClient(this._config.Summerization));
        var sessionMemory = _internalExport.GetService<ISessionMemory>()
                            ?? new SessionMemory(
                                _internalExport.GetRequiredService<ISqlSugarClient>(),
                                _loggerFactory);
        var tokenCounter = _internalExport.GetService<ITokenCounter>()
                            ?? new TiktokenCounter(_loggerFactory);
        var extractionClient = _internalExport.GetKeyedService<IChatClient>("extraction")
                                ?? (string.IsNullOrWhiteSpace(this._config.Extraction?.ApiKey)
                                    ? CreateChatClient(this._config)
                                    : CreateChatClient(this._config.Extraction));
        var llmLongMemoryExtractor = _internalExport.GetService<LLMLongMemoryExtractor>()
                                        ?? new LLMLongMemoryExtractor(
                                            extractionClient,
                                            Options.Create(this._config.Memory),
                                            _loggerFactory);
        var longMemory = _internalExport.GetService<ILongMemory>()
                         ?? new LongMemory(
                             llmLongMemoryExtractor,
                             sessionMemory,
                             tokenCounter,
                             Options.Create(this._config.Memory),
                             _internalExport.GetRequiredService<ISqlSugarClient>(),
                             _loggerFactory);
        var reducer = _internalExport.GetService<EveConvChatReducer>()
                      ?? new EveConvChatReducer(
                          summarizationClient,
                          sessionMemory,
                          tokenCounter,
                          Options.Create(this._config.Memory),
                          _loggerFactory);

        return new MemoryService(
            _internalExport.GetRequiredService<IRecentMemory>(),
            sessionMemory,
            longMemory,
            reducer,
            tokenCounter,
            Options.Create(this._config.Memory),
            _loggerFactory);
    }

    private IChatClient CreateChatClient(ChatConfig config)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Creating ChatClient for model {ModelName} with endpoint {Endpoint}", config.ModelName, config.Endpoint);
        }
        return new OpenAI.Chat.ChatClient(
                config.ModelName,
                new ApiKeyCredential(config.ApiKey),
                new()
                {
                    Endpoint = new Uri(config.Endpoint),
                })
            .AsIChatClient();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _chatClient.Dispose();
        GC.SuppressFinalize(this);
        _disposed = true;
    }
}
