using System.Text;
using EveConv.Channel.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NetMQ;
using NetMQ.Sockets;

namespace EveConv.Channel.Client;

public class ChannelPublisher : IDisposable
{
    private bool _disposed = false;

    private readonly ChannelPublisherConfig _config;
    private readonly ILogger<ChannelPublisher> _logger;
    private readonly PublisherSocket _publisherSocket;

    public ChannelPublisher(ChannelPublisherConfig config, ILogger<ChannelPublisher>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Valid();
        this._config = config;
        this._logger = logger ?? NullLogger<ChannelPublisher>.Instance;
        this._publisherSocket = new(_config.BindAddress);
        this._publisherSocket.UpdateOptions(_config);
    }

    private readonly Lock _publishLock = new();

    public void Publish(string source, string eventName, object? payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        var topic = new NetMQFrame($"{source.ToLowerInvariant()}:{eventName.ToLowerInvariant()}", Encoding.UTF8);
        var e = payload switch
        {
            null => NetMQFrame.Empty,
            NetMQFrame p => p,
            _ => new NetMQFrame(payload.ToMsgPack())
        };
        var msg = new NetMQMessage();
        msg.Append(topic);
        msg.Append(e);
        lock (_publishLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            this._publisherSocket.SendMultipartMessage(msg);
        }
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Publish with topic {source}:{event} [{len}]", source, eventName, e.BufferSize);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        lock (_publishLock)
        {
            if (!_publisherSocket.IsDisposed)
            {
                _publisherSocket.Dispose();
            }
        }
        GC.SuppressFinalize(this);
        _disposed = true;
    }
}
