using System.Text;
using EveConv.Channel.Common;
using NetMQ;
using NetMQ.Sockets;

namespace EveConv.Channel.Client.Tests;

public class ChannelPublisherTests : IDisposable
{
    private readonly XPublisherSocket _xpub;
    private readonly XSubscriberSocket _xsub;
    private readonly TestIntermediary _intermediary;

    public ChannelPublisherTests()
    {
        _xpub = new("@tcp://127.0.0.1:0");
        _xsub = new("@tcp://127.0.0.1:0");
        _intermediary = new(_xsub, _xpub);
    }

    [Fact]
    public void Publish_WithPayload_Success()
    {
        using var publisher = new ChannelPublisher(new()
            { BindAddress = ">" + (_xsub.Options.LastEndpoint ?? string.Empty) });
        using var subscriber = new SubscriberSocket(">" + (_xpub.Options.LastEndpoint ?? string.Empty));
        subscriber.Subscribe("test:eve", Encoding.UTF8);
        Thread.Sleep(500);
        publisher.Publish("Test", "Event", "HELLO");
        Thread.Sleep(500);
        NetMQMessage? msg = null;
        var res = subscriber.TryReceiveMultipartMessage(TimeSpan.FromSeconds(1), ref msg, 2);
        Assert.True(res);
        Assert.NotNull(msg);
        Assert.Equal(2, msg.FrameCount);
        Assert.Equal("test:event", msg[0].ConvertToString(Encoding.UTF8));
        Assert.Equal("HELLO", msg[1].Buffer.FromMsgPack<string>());
    }

    public void Dispose()
    {
        _intermediary.Dispose();
        _xpub.Dispose();
        _xsub.Dispose();
        GC.SuppressFinalize(this);
    }
}
