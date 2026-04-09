using NetMQ;

namespace EveConv.Channel.Client;

public class ChannelSocketConfig
{
    public string BindAddress { get; init; } = string.Empty;
    public int? SendHighWatermark { get; init; }
    public int? ReceiveHighWatermark { get; init; }

    internal virtual void Valid()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(BindAddress);
    }
}

internal static class NetMQSocketExtensions
{
    internal static void UpdateOptions(this NetMQSocket socket, ChannelSocketConfig? config)
    {
        if (config is null)
        {
            return;
        }
        if (config.SendHighWatermark is > 0)
        {
            socket.Options.SendHighWatermark = config.SendHighWatermark.Value;
        }
        if (config.ReceiveHighWatermark is > 0)
        {
            socket.Options.ReceiveHighWatermark = config.ReceiveHighWatermark.Value;
        }
    }
}
