using EveConv.Channel.Common;
using NetMQ;
using NetMQ.Sockets;

namespace EveConv.Channel.Client.Tests;

public class ChannelPubSubTests : IDisposable
{
    private readonly XPublisherSocket _xpub;
    private readonly XSubscriberSocket _xsub;
    private readonly TestIntermediary _intermediary;

    public ChannelPubSubTests()
    {
        _xpub = new("@tcp://127.0.0.1:0");
        _xsub = new("@tcp://127.0.0.1:0");
        _intermediary = new(_xsub, _xpub);
    }

    [Fact]
    public void PubSub_WithPayload_Success()
    {
        using var publisher = new ChannelPublisher(new()
        {
            BindAddress = ">" + (_xsub.Options.LastEndpoint ?? string.Empty),
        });
        using var subscriber = new ChannelSubscriber(new()
        {
            BindAddress = ">" + (_xpub.Options.LastEndpoint ?? string.Empty),
        });
        AutoResetEvent received = new(false);
        string? receivedPayload = null;
        subscriber.Subscribe<string>("test", "event", (i) =>
        {
            receivedPayload = i?.Payload;
            received.Set();
        });
        for (var _ = 0; _ < 10; _++)
        {
            Thread.Sleep(200);
            publisher.Publish("test", "event", "HELLO");
            var res = received.WaitOne(200);
            if (res)
            {
                Assert.Equal("HELLO", receivedPayload);
                return;
            }
        }
        Assert.Fail("Failed to receive message after 10 attempts.");
    }

    public void Dispose()
    {
        _intermediary.Dispose();
        _xpub.Dispose();
        _xsub.Dispose();
        GC.SuppressFinalize(this);
    }
}
