using EveConv.Channel.Common;
using NetMQ;
using NetMQ.Sockets;

namespace EveConv.Channel.Client.Tests;

internal sealed class TestIntermediary : IDisposable
{
    private readonly XPublisherSocket _xpub;
    private readonly XSubscriberSocket _xsub;
    private readonly NetMQPoller _poller;

    public TestIntermediary(XSubscriberSocket xsub, XPublisherSocket xpub)
    {
        _xsub = xsub;
        _xpub = xpub;
        _poller = [_xsub, _xpub];
        _xsub.ReceiveReady += OnXSubReady;
        _xpub.ReceiveReady += OnXPubReady;
        _ = new Proxy(_xsub, _xpub, null, _poller);
        _poller.RunAsync();
        _poller.WaitForStart(TimeSpan.FromSeconds(5));
    }

    #region poller
    
    private void OnXSubReady(object? sender, NetMQSocketEventArgs e) =>
        ProxyBetween(_xsub, _xpub);

    private void OnXPubReady(object? sender, NetMQSocketEventArgs e) =>
        ProxyBetween(_xpub, _xsub);

    /// <summary>
    /// Copy messages from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    /// <seealso cref="Proxy.ProxyBetween"/>
    private static void ProxyBetween(IReceivingSocket from, IOutgoingSocket to, IOutgoingSocket? control = null)
    {
        var msg = new Msg();
        msg.InitEmpty();
        var copy = new Msg();
        copy.InitEmpty();
        while (true)
        {
            from.Receive(ref msg);
            var more = msg.HasMore;
            if (control != null)
            {
                copy.Copy(ref msg);
                control.Send(ref copy, more);
            }
            to.Send(ref msg, more);
            if (!more)
                break;
        }
        copy.Close();
        msg.Close();
    }
    
    #endregion

    public void Dispose()
    {
        _poller.StopAsync();
        _poller.Dispose();
        _xpub.ReceiveReady -= OnXPubReady;
        _xsub.ReceiveReady -= OnXSubReady;
    }
}
