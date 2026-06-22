using EveConv.Memory.Config;

namespace EveConv.Plugins.OpenAIApiClient;

public class Config : ChatConfig
{
    public ChatConfig Summerization { get; set; } = new ChatConfig();
    public ChatConfig Extraction { get; set; } = new ChatConfig();
    public MemoryConfiguration Memory { get; set; } = new MemoryConfiguration();
}

public class ChatConfig
{
    public string Endpoint { get; init; } = "https://api.openai.com/v1/chat";
    public string ApiKey { get; init; } = "";
    public string ModelName { get; init; } = "gpt-5.5";

    internal void Valid()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(this.ApiKey);
    }
}
