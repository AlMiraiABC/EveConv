namespace EveConv.Channel.Client;

public class ChannelClientConfig : ChannelSocketConfig
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public int WaitQueueSize { get; set; } = -1;
}
