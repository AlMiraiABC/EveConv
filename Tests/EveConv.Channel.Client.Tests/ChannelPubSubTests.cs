using NetMQ;
using NetMQ.Sockets;

namespace EveConv.Channel.Client.Tests;

public class ChannelPubSubTests : IDisposable
{
    private readonly XPublisherSocket _xpub;
    private readonly XSubscriberSocket _xsub;
    private readonly Proxy _proxy;

    public ChannelPubSubTests()
    {
        _xpub = new XPublisherSocket("@tcp://127.0.0.1:0");
        _xsub = new XSubscriberSocket("@tcp://127.0.0.1:0");
        _proxy = new Proxy(_xsub, _xpub);
        Task.Run(_proxy.Start);
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
        subscriber.Subscribe<string>("test", "event", (i) =>
        {
            Assert.NotNull(i);
            Assert.Equal("HELLO", i.Payload);
            received.Set();
        });
        Thread.Sleep(500);
        publisher.Publish("test", "event", "HELLO");
        var res = received.WaitOne(500);
        Assert.True(res);
    }

    public void Dispose()
    {
        _proxy.Stop();
        _xpub.Dispose();
        _xsub.Dispose();
        GC.SuppressFinalize(this);
    }
}
