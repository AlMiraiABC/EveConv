using EveConv.Channel.Common;
using EveConv.Channel.Server.obj;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NetMQ;
using NetMQ.Sockets;

namespace EveConv.Channel.Server;

/// <summary>
/// Intermediary for PUB-SUB mode.
/// </summary>
public class ChannelIntermediary : IDisposable
{
    private bool _disposed = false;
    
    private static readonly TimeSpan POLLER_START_TIMEOUT = TimeSpan.FromSeconds(5);

    private readonly ILogger<ChannelIntermediary> _logger;
    private readonly ChannelIntermediaryConfig _config;

    private readonly XPublisherSocket _publisher;
    private readonly XSubscriberSocket _subscriber;
    private readonly NetMQPoller _poller;

    public ChannelIntermediary(ChannelIntermediaryConfig config, ILogger<ChannelIntermediary>? logger = null)
    {
        this._logger = logger ?? NullLogger<ChannelIntermediary>.Instance;
        this._config = config;
        this._publisher = new(config.PublisherAddress);
        this._subscriber = new(config.SubscriberAddress);
        this._poller = [_publisher, _subscriber];
        this._publisher.ReceiveReady += OnPublisherReceiveReady;
        this._subscriber.ReceiveReady += OnSubscriberReceiveReady;
        _ = new Proxy(_subscriber, _publisher, null, _poller);
    }

    public string PublisherAddress => this._publisher.Options.LastEndpoint ?? this._config.PublisherAddress;
    public string SubscriberAddress => this._subscriber.Options.LastEndpoint ?? this._config.SubscriberAddress;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(this._disposed, this);
        if (_poller.IsRunning)
        {
            return;
        }
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Starting intermediary between {pub} {sub}", this.PublisherAddress, this.SubscriberAddress);
        }
        this._poller.RunAsync();
        this._poller.WaitForStart(POLLER_START_TIMEOUT);
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Intermediary started between {pub} {sub}", this.PublisherAddress, this.SubscriberAddress);
        }
    }

    #region events

    private void OnPublisherReceiveReady(object? sender, NetMQSocketEventArgs e) =>
        ProxyBetween(this._publisher, this._subscriber, null);

    private void OnSubscriberReceiveReady(object? sender, NetMQSocketEventArgs e) =>
        ProxyBetween(this._subscriber, this._publisher, null);

    /// <summary>
    /// Copy messages from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    /// <seealso cref="Proxy.ProxyBetween"/>
    private static void ProxyBetween(IReceivingSocket from, IOutgoingSocket to, IOutgoingSocket? control)
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
        if (_disposed)
        {
            return;
        }
        if (_poller.IsRunning)
        {
            _poller.StopAsync();
        }
        if (!_poller.IsDisposed)
        {
            _poller.Dispose();
        }
        _publisher.ReceiveReady -= OnPublisherReceiveReady;
        if (!_publisher.IsDisposed)
        {
            _publisher.Dispose();
        }
        _subscriber.ReceiveReady -= OnSubscriberReceiveReady;
        if (!_subscriber.IsDisposed)
        {
            _subscriber.Dispose();
        }
        GC.SuppressFinalize(this);
        _disposed = true;
    }
}
