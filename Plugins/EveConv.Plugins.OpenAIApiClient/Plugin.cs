using System.ClientModel;
using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.Memory;
using EveConv.Memory.Config;
using EveConv.Memory.Extensions;
using EveConv.Memory.Services;
using EveConv.Plugins.Api;
using EveConv.Plugins.Api.Abstraction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        this._chatClient = new ChatClientBuilder(openAIChatClient)
            .UseMemory(
                GetChatReducer(),
                _internalExport.GetRequiredService<IRecentMemory>(),
                _internalExport.GetRequiredService<ILongMemory>(),
                loggerFactory)
            .Build();
    }

    private EveConvChatReducer GetChatReducer()
    {
        var client = _internalExport.GetKeyedService<IChatClient>("summarization")
                     ?? (string.IsNullOrWhiteSpace(this._config.Summerization?.ApiKey)
                         ? CreateChatClient(this._config)
                         : CreateChatClient(this._config.Summerization));
        return new EveConvChatReducer(client,
            _internalExport.GetRequiredService<ISessionMemory>(),
            _internalExport.GetRequiredService<ITokenCounter>(),
            Options.Create(this._config.Memory), _loggerFactory);
    }

    private static IChatClient CreateChatClient(ChatConfig config)
    {
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
