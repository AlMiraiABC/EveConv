using System.Text;
using EveConv.Channel.Common;
using NetMQ;
using NetMQ.Sockets;

namespace EveConv.Channel.Client.Tests;

public class ChannelSubscriberTests : IDisposable
{
    private readonly XPublisherSocket _xpub;
    private readonly XSubscriberSocket _xsub;
    private readonly TestIntermediary _intermediary;

    public ChannelSubscriberTests()
    {
        _xpub = new("@tcp://127.0.0.1:0");
        _xsub = new("@tcp://127.0.0.1:0");
        _intermediary = new(_xsub, _xpub);
    }

    [Fact]
    public void Subscribe_WithPayload_Success()
    {
        using var subscriber = new ChannelSubscriber(new()
            { BindAddress = ">" + (_xpub.Options.LastEndpoint ?? string.Empty) });
        using var publisher = new PublisherSocket(">" + (_xsub.Options.LastEndpoint ?? string.Empty));
        var received = new AutoResetEvent(false);
        string? receivedPayload = null;
        subscriber.Subscribe<string>("Test", "Event", (i) =>
        {
            receivedPayload = i?.Payload;
            received.Set();
        });
        Thread.Sleep(500);
        publisher.SendMoreFrame("test:event")
            .SendFrame("HELLO".ToMsgPack());
        var res = received.WaitOne(500);
        Assert.True(res);
        Assert.Equal("HELLO",  receivedPayload);
    }

    [Fact]
    public void Unsubscribe_Success()
    {
        using var subscriber = new ChannelSubscriber(new()
            { BindAddress = ">" + (_xpub.Options.LastEndpoint ?? string.Empty) });
        using var publisher = new PublisherSocket(">" + (_xsub.Options.LastEndpoint ?? string.Empty));
        var received = new AutoResetEvent(false);
        subscriber.Subscribe<string>("Test", "Event", (_) =>
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
        _intermediary.Dispose();
        _xpub.Dispose();
        _xsub.Dispose();
        GC.SuppressFinalize(this);
    }
}
