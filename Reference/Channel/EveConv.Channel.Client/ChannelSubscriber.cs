using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using EveConv.Channel.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NetMQ;
using NetMQ.Sockets;

namespace EveConv.Channel.Client;

public class ChannelSubscriber : IDisposable
{
    private bool _disposed = false;

    private const int FRAME_COUNT = 2; // topic message
    private static readonly TimeSpan DEQUEUE_TIMEOUT = TimeSpan.Zero;
    private static readonly Encoding STR_ENCODING = Encoding.UTF8;

    private readonly ILogger<ChannelSubscriber> _logger;
    private readonly ChannelSubscriberConfig _config;
    private readonly SubscriberSocket _subscriberSocket;
    private readonly NetMQPoller _poller;
    private readonly NetMQQueue<(bool Op, string Topic)> _opQueue; // OP: true->Subscribe, false->Unsubscribe

    private readonly ConcurrentDictionary<string, (Type? Type, Action<EventInfo<object?>?> Handler)> _handlers =
        new(StringComparer.OrdinalIgnoreCase);

    public ChannelSubscriber(ChannelSubscriberConfig config, ILogger<ChannelSubscriber>? logger = null)
    {
        config.Valid();
        this._config = config;
        this._logger = logger ?? NullLogger<ChannelSubscriber>.Instance;
        this._subscriberSocket = new(_config.BindAddress);
        this._subscriberSocket.UpdateOptions(_config);
        this._subscriberSocket.ReceiveReady += SubscriberSocketReceiveReadyHandle;
        this._opQueue = new();
        this._opQueue.ReceiveReady += OpQueueReceiveReadyHandle;
        this._poller = [_subscriberSocket, _opQueue];
        this._poller.RunAsync();
    }

    /// <summary>
    /// Subscribe an event.
    /// </summary>
    /// <param name="source">Event source.</param>
    /// <param name="eventName">Event name.</param>
    /// <param name="handler">Callback handler.</param>
    /// <typeparam name="T">Type of event args.</typeparam>
    public void Subscribe<T>(string source, string eventName, Action<EventInfo<T?>?> handler)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var topic = CreateTopic(source, eventName);
        this._handlers[topic] = (typeof(T), (e) => handler(e is null ? null : EventInfo<T>.DownCast<T, object>(e)));
        this._opQueue.Enqueue((true, topic));
    }

    /// <summary>
    /// Subscribe an event.
    /// </summary>
    /// <param name="source">Event source.</param>
    /// <param name="eventName">Event name.</param>
    /// <param name="handler">Callback handler.</param>
    /// <param name="payloadType">Type of event args.</param>
    public void Subscribe(string source, string eventName, Action<EventInfo<object?>?> handler, Type? payloadType = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var topic = CreateTopic(source, eventName);
        this._handlers[topic] = (payloadType, handler);
        this._opQueue.Enqueue((true, topic));
    }

    /// <summary>
    /// Unsubscribe an event.
    /// </summary>
    /// <param name="source">Event source.</param>
    /// <param name="eventName">Event name.</param>
    public void Unsubscribe(string source, string eventName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var topic = CreateTopic(source, eventName);
        this._handlers.TryRemove(topic, out _);
        this._opQueue.Enqueue((false, topic));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string CreateTopic(string source, string eventName) =>
        $"{source.ToLowerInvariant()}:{eventName.ToLowerInvariant()}";

    private void OpQueueReceiveReadyHandle(object? sender, NetMQQueueEventArgs<(bool Op, string Topic)> e)
    {
        while (e.Queue.TryDequeue(out var item, DEQUEUE_TIMEOUT))
        {
            if (item.Op)
            {
                this._subscriberSocket.Subscribe(item.Topic, STR_ENCODING);
            }
            else
            {
                this._subscriberSocket.Unsubscribe(item.Topic, STR_ENCODING);
            }
        }
    }

    private void SubscriberSocketReceiveReadyHandle(object? sender, NetMQSocketEventArgs e)
    {
        NetMQMessage? msg = new();
        while (e.Socket.TryReceiveMultipartMessage(DEQUEUE_TIMEOUT, ref msg, FRAME_COUNT))
        {
            if (msg is null || msg.IsEmpty)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Received empty message.");
                }
                continue;
            }
            var topic = msg[0].ConvertToString(STR_ENCODING);
            if (string.IsNullOrWhiteSpace(topic))
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Received message with empty topic");
                }
                continue;
            }
            if (!_handlers.TryGetValue(topic, out var handler))
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Topic `{topic}` is not subscribed", topic);
                }
                continue;
            }
            var (source, eventName) = topic.ParseTopic();
            object? payload = null;
            var ty = handler.Type;
            if (ty is not null && msg.FrameCount > 1)
            {
                var rawPayload = msg[1];
                payload = ty == typeof(NetMQFrame)
                    ? rawPayload
                    : rawPayload.Buffer.FromMsgPack(ty);
            }
            var eventInfo = new EventInfo<object?>(source, eventName, topic, payload);
            handler.Handler.Invoke(eventInfo);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        if (_poller.IsRunning)
        {
            _poller.Stop();
        }
        if (!_poller.IsDisposed)
        {
            _poller.Dispose();
        }
        if (!_opQueue.IsDisposed)
        {
            _opQueue.Dispose();
        }
        if (!_subscriberSocket.IsDisposed)
        {
            _subscriberSocket.Dispose();
        }
        GC.SuppressFinalize(this);
        this._disposed = true;
    }
}
