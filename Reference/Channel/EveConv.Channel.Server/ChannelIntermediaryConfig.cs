namespace EveConv.Channel.Server.obj;

public class ChannelIntermediaryConfig
{
    public string PublisherAddress { get; init; } = "@tcp://localhost:0";
    public string SubscriberAddress { get; init; } = "@tcp://localhost:0";
}
