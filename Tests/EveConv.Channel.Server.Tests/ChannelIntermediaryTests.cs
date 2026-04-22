using System.Text;
using EveConv.Channel.Common;
using NetMQ;
using NetMQ.Sockets;

namespace EveConv.Channel.Server.Tests;

public class ChannelIntermediaryTests : IDisposable
{
    private readonly ChannelIntermediary _server = new(new());

    [Fact]
    public void Start_Subscribe_Success()
    {
        _server.Start();
        var pubAddr = this._server.PublisherAddress;
        var subAddr = this._server.SubscriberAddress;
        using var sub = new SubscriberSocket(">" + pubAddr);
        using var pub = new PublisherSocket(">" + subAddr);
        sub.Subscribe("Test/Ev", Encoding.UTF8);
        Thread.Sleep(TimeSpan.FromMilliseconds(500));
        var exp = new NetMQMessage();
        exp.Append("Test/Event", Encoding.UTF8);
        exp.Append("Hello World".ToMsgPack());
        pub.SendMultipartMessage(exp);
        Thread.Sleep(TimeSpan.FromMilliseconds(100));
        pub.SendMultipartMessage(exp); // may lose, try again
        NetMQMessage? act = null;
        var success = sub.TryReceiveMultipartMessage(TimeSpan.FromSeconds(1), ref act, 2);
        Assert.True(success);
        Assert.NotNull(act);
        Assert.Equal(exp.FrameCount, act.FrameCount);
        Assert.Equal("Test/Event", act[0].ConvertToString(Encoding.UTF8));
        Assert.Equal("Hello World", act[1].Buffer.FromMsgPack<string>());
    }

    [Fact]
    public void Start_Subscribe_Unmatch()
    {
        _server.Start();
        var pubAddr = this._server.PublisherAddress;
        var subAddr = this._server.SubscriberAddress;
        using var sub = new SubscriberSocket(">" + pubAddr);
        using var pub = new PublisherSocket(">" + subAddr);
        sub.Subscribe("Test/Evd", Encoding.UTF8); // unexists
        Thread.Sleep(TimeSpan.FromMilliseconds(500));
        var exp = new NetMQMessage();
        exp.Append("Test/Event", Encoding.UTF8);
        exp.Append("Hello World".ToMsgPack());
        pub.SendMultipartMessage(exp);
        Thread.Sleep(TimeSpan.FromMilliseconds(100));
        pub.SendMultipartMessage(exp); // may lose, try again
        NetMQMessage? act = null;
        var success = sub.TryReceiveMultipartMessage(TimeSpan.FromSeconds(1), ref act, 2);
        Assert.False(success);
    }

    public void Dispose()
    {
        _server.Dispose();
        GC.SuppressFinalize(this);
    }
}
