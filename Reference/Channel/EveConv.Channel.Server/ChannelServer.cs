using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using System.Text;
using NetMQ;
using NetMQ.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EveConv.Channel.Server
{
    /// <summary>
    /// Handler to process request.
    /// </summary>
    /// <param name="query">Query include path and parameters.</param>
    /// <param name="payload">Request payload body.</param>
    /// <returns>Response payload body.</returns>
    public delegate object? ChannelHandler(string query, ReadOnlyMemory<byte>? payload);

    /// <summary>
    /// Channel server side to process requests.
    /// </summary>
    /// <remarks>
    ///     <para>Based on ZeroMQ ROUTE+POLLING mode.</para>
    ///     <para>Each request frames must contains {ID} {Empty} {Query(string)} {Payload(msgpack}.</para>
    ///     <para>Each response frames must contains {ID} {Empty} {Payload(msgpack)} {Error(msgpack)}.</para>
    /// </remarks>
    public class ChannelServer : IDisposable
    {
        private const int FRAME_COUNT = 4;
        private static readonly TimeSpan DEQUEUE_TIMEOUT = TimeSpan.Zero;
        private bool _disposed = false;

        private readonly ChannelServerConfig _config;
        private readonly ILogger<ChannelServer> _logger;

        private readonly RouterSocket _router;
        private readonly NetMQQueue<NetMQMessage> _inQueue;
        private readonly NetMQQueue<NetMQMessage> _outQueue;
        private readonly NetMQPoller _poller;

        private readonly ConcurrentDictionary<string, ChannelHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);

        public ChannelServer(ChannelServerConfig config, ILogger<ChannelServer>? logger = null)
        {
            ArgumentNullException.ThrowIfNull(config);
            config.Valid();
            this._config = config;
            this._logger = logger ?? NullLogger<ChannelServer>.Instance;
            this._router = new(_config.BindAddress);
            this._inQueue = new(_config.QueueSize);
            this._outQueue = new(_config.QueueSize);
            this._poller = [_router, _inQueue, _outQueue];
            this._router.ReceiveReady += RouterReceiveReadyHandler;
            this._inQueue.ReceiveReady += InQueueReceiveReadyHandler;
            this._outQueue.ReceiveReady += OutQueueReceiveReadyHandler;
            this._poller.RunAsync(Guid.NewGuid().ToString(), true);
        }

        /// <summary>
        /// Add a handler with specified path to process request. If the path already exists, the handler will be replaced.
        /// </summary>
        /// <param name="path">Path of query without parameters.</param>
        /// <param name="handler">Handler callback to process request.</param>
        /// <returns><see langword="true"/> if added successfully. Otherwise, <see langword="false"/>.</returns>
        public bool RegisterHandler(string path, ChannelHandler handler)
        {
            ArgumentNullException.ThrowIfNull(path);
            ArgumentNullException.ThrowIfNull(handler);
            _handlers[path] = handler;
            return true;
        }

        /// <summary>
        /// Remove a handler by specified path. If the path does not registered, nothing will be removed.
        /// </summary>
        /// <param name="path">Path of query without parameters that registered.</param>
        /// <returns><see langword="true"/> if removed successfully. Otherwise, <see langword="false"/>.</returns>
        public bool UnregisterHandler(string path)
        {
            ArgumentNullException.ThrowIfNull(path);
            return _handlers.TryRemove(path, out _);
        }

        /// <summary>
        /// Remove all handlers.
        /// </summary>
        public void ClearHandlers()
        {
            _handlers.Clear();
        }

        /// <summary>
        /// Get a snapshot of all registered handlers.
        /// </summary>
        /// <returns>A dictionary of path and handler pairs.</returns>
        public IDictionary<string, ChannelHandler> Handlers()
        {
            return _handlers.ToImmutableDictionary();
        }

        /// <summary>
        /// Event for <see cref="_inQueue.ReceiveReady"/>
        /// </summary>
        private void InQueueReceiveReadyHandler(object? sender, NetMQQueueEventArgs<NetMQMessage> e)
        {
            while (e.Queue.TryDequeue(out var req, DEQUEUE_TIMEOUT))
            {
                if (req is null || req.IsEmpty)
                {
                    continue;
                }
                var identity = req[0];
                var query = _config.QueryEncoding.GetString(req[2].ToByteArray());
                var payload = req.FrameCount > 3 ? req[3].ToByteArray() : null;
                NetMQMessage response;
                try
                {
                    var respPayload = DispatchHandler(query, payload).ToMsgPack();
                    response = CreateMQMessage(identity, new NetMQFrame(respPayload), NetMQFrame.Empty);
                }
                catch (HttpRequestException ex)
                {
                    if (_logger.IsEnabled(LogLevel.Error))
                    {
                        _logger.LogError(ex, "Error occurred while processing request {query} from socket {addr}.",
                            query, _config.BindAddress);
                    }
                    var errPayload = ErrorHandler.Handle(ex).ToMsgPack();
                    response = CreateMQMessage(identity, NetMQFrame.Empty, new NetMQFrame(errPayload));
                }
                catch (Exception ex)
                {
                    if (_logger.IsEnabled(LogLevel.Error))
                    {
                        _logger.LogError(ex, "Error occurred while processing request {query} from socket {addr}.",
                            query, _config.BindAddress);
                    }
                    var errPayload = ErrorHandler.Handle(ex, HttpStatusCode.InternalServerError).ToMsgPack();
                    response = CreateMQMessage(identity, NetMQFrame.Empty, new NetMQFrame(errPayload));
                }
                _outQueue.Enqueue(response);
            }
        }

        /// <summary>
        /// Event for <see cref="_outQueue.ReceiveReady"/>
        /// </summary>
        private void OutQueueReceiveReadyHandler(object? sender, NetMQQueueEventArgs<NetMQMessage> e)
        {
            while (e.Queue.TryDequeue(out var resp, DEQUEUE_TIMEOUT))
            {
                if (resp is null || resp.IsEmpty)
                {
                    continue;
                }
                _router.SendMultipartMessage(resp);
            }
        }

        /// <summary>
        /// Event for <see cref="_router.ReceiveReady"/>.
        /// </summary>
        private void RouterReceiveReadyHandler(object? sender, NetMQSocketEventArgs e)
        {
            NetMQMessage? req = new();
            if (!e.Socket.TryReceiveMultipartMessage(ref req, FRAME_COUNT) || req is null || req.IsEmpty)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Cannot get message from socket {addr}.", _config.BindAddress);
                }
                return;
            }
            var id = req[0];
            // <id> <empty> <query> [payload]
            if (req.FrameCount is < 3 or > 4 || !req[1].IsEmpty || req[2].IsEmpty)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Invalid message received from socket {addr}.", _config.BindAddress);
                }
                e.Socket.SendMultipartMessage(
                    CreateMQMessage(id, NetMQFrame.Empty, new(ErrorHandler.BadRequestMsgPack)));
                return;
            }
            _inQueue.Enqueue(req);
        }

        /// <summary>
        /// Dispatch to matched handler.
        /// </summary>
        /// <param name="query">The request query string.</param>
        /// <param name="payload">The request payload body in msgpack format.</param>
        /// <returns>The response payload body.</returns>
        /// <exception cref="KeyNotFoundException">Path not matched. Register it firstly.</exception>
        private object? DispatchHandler(string query, ReadOnlyMemory<byte>? payload)
        {
            var path = query.GetPath();
            if (ReservedPath.TryHandle(path, payload, out var resp))
            {
                return resp;
            }
            if (_handlers.TryGetValue(path, out var handler))
            {
                return handler(query, payload);
            }
            throw new HttpRequestException("Path not found: " + path, null, HttpStatusCode.NotFound);
        }

        /// <summary>
        /// Create a zmq multipart message.
        /// </summary>
        /// <remarks>{Identity} {Empty} {Payload...}</remarks>
        private static NetMQMessage CreateMQMessage(NetMQFrame identity, NetMQFrame payload, NetMQFrame err)
        {
            var msg = new NetMQMessage(FRAME_COUNT);
            msg.Append(identity);
            msg.AppendEmptyFrame();
            msg.Append(payload);
            msg.Append(err);
            return msg;
        }

        #region dispose

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            if (this._poller.IsRunning)
            {
                this._poller.Stop();
            }
            if (!this._poller.IsDisposed)
            {
                this._poller.Dispose();
            }
            _inQueue.ReceiveReady -= InQueueReceiveReadyHandler;
            if (!this._inQueue.IsDisposed)
            {
                ClearInQueue();
                this._inQueue.Dispose();
            }
            _outQueue.ReceiveReady -= OutQueueReceiveReadyHandler;
            if (!this._outQueue.IsDisposed)
            {
                ClearOutQueue();
                this._outQueue.Dispose();
            }
            _router.ReceiveReady -= RouterReceiveReadyHandler;
            if (!this._router.IsDisposed)
            {
                this._router.Dispose();
            }
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        private void ClearInQueue()
        {
            while (_inQueue.TryDequeue(out var msg, DEQUEUE_TIMEOUT))
            {
                if (msg is null || msg.IsEmpty)
                {
                    continue;
                }
                var id = msg[0];
                _router.SendMultipartMessage(CreateMQMessage(id, NetMQFrame.Empty,
                    new(ErrorHandler.ServiceUnavailableMsgPack)));
            }
        }

        private void ClearOutQueue()
        {
            while (_outQueue.TryDequeue(out var msg, DEQUEUE_TIMEOUT))
            {
                if (msg is null || msg.IsEmpty)
                {
                    continue;
                }
                _router.SendMultipartMessage(msg);
            }
        }

        #endregion
    }
}
