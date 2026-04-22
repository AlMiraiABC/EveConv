using System.Text;
using EveConv.Channel.Common;
using NetMQ;
using NetMQ.Sockets;

namespace EveConv.Channel.Client.Tests;

public class ChannelSubscriberTests : IDisposable
{
    private readonly XPublisherSocket _xpub;
    private readonly XSubscriberSocket _xsub;
    private readonly Proxy _proxy;

    public ChannelSubscriberTests()
    {
        _xpub = new XPublisherSocket("@tcp://127.0.0.1:0");
        _xsub = new XSubscriberSocket("@tcp://127.0.0.1:0");
        _proxy = new Proxy(_xsub, _xpub);
        Task.Run(_proxy.Start);
    }

    [Fact]
    public void Subscribe_WithPayload_Success()
    {
        using var subscriber = new ChannelSubscriber(new()
        { BindAddress = ">" + (_xpub.Options.LastEndpoint ?? string.Empty) });
        using var publisher = new PublisherSocket(">" + (_xsub.Options.LastEndpoint ?? string.Empty));
        var received = new AutoResetEvent(false);
        subscriber.Subscribe<string>("Test", "Event", (i) =>
        {
            Assert.NotNull(i);
            Assert.Equal("HELLO", i.Payload);
            received.Set();
        });
        Thread.Sleep(500);
        publisher.SendMoreFrame("test:event")
            .SendFrame("HELLO".ToMsgPack());
        var res = received.WaitOne(500);
        Assert.True(res);
    }

    [Fact]
    public void Unsubscribe_Success()
    {
        using var subscriber = new ChannelSubscriber(new()
        { BindAddress = ">" + (_xpub.Options.LastEndpoint ?? string.Empty) });
        using var publisher = new PublisherSocket(">" + (_xsub.Options.LastEndpoint ?? string.Empty));
        var received = new AutoResetEvent(false);
        subscriber.Subscribe<string>("Test", "Event", (i) =>
        {
            Assert.Fail("Shouldn't receive.");
            received.Set();
        });
        Thread.Sleep(500);
        subscriber.Unsubscribe("Test", "Event");
        publisher.SendMoreFrame("test:event")
            .SendFrame("HELLO".ToMsgPack());
        var res = received.WaitOne(1000);
        Assert.False(res);
    }

    public void Dispose()
    {
        _proxy.Stop();
        _xpub.Dispose();
        _xsub.Dispose();
        GC.SuppressFinalize(this);
    }
}
