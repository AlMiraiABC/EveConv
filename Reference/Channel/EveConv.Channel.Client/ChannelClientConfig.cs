namespace EveConv.Channel.Client;

public class ChannelClientConfig
{
    public string BindAddress { get; set; } = string.Empty;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public int WaitQueueSize { get; set; } = -1;

    internal void Valid()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(BindAddress);
    }
}
