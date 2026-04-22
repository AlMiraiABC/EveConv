using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using System.Text;
using EveConv.Channel.Common;
using NetMQ;
using NetMQ.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EveConv.Channel.Server
{
    /// <summary>
    /// Handler to process request for REQ-RESP mode.
    /// </summary>
    /// <param name="query">Query include path and parameters.</param>
    /// <param name="payload">Request payload body.</param>
    /// <returns>Response payload body.</returns>
    public delegate object? ChannelHandler(string query, object? payload);

    /// <summary>
    /// Channel server side to process requests.
    /// </summary>
    /// <remarks>
    ///     <para>Based on ZeroMQ ROUTE+POLLING mode.</para>
    ///     <para>Each request frames must contain {ID} {Empty} {RequestId} {Query(string)} {Payload(msgpack)}.</para>
    ///     <para>Each response frames must contain {ID} {Empty} {RequestId} {Payload(msgpack)} {Error(msgpack)}.</para>
    /// </remarks>
    public class ChannelServer : IDisposable
    {
        private const int FRAME_COUNT = 4;
        private static readonly TimeSpan DEQUEUE_TIMEOUT = TimeSpan.Zero;
        private static readonly TimeSpan POLLER_START_TIMEOUT = TimeSpan.FromSeconds(5);
        private bool _disposed = false;

        private readonly ChannelServerConfig _config;
        private readonly ILogger<ChannelServer> _logger;

        private readonly RouterSocket _router;
        private readonly NetMQQueue<RequestInQueue> _inQueue;
        private readonly NetMQQueue<ResponseOutQueue> _outQueue;
        private readonly NetMQPoller _poller;

        private readonly ConcurrentDictionary<string, (ChannelHandler Handler, Type? PayloadType, Type? RespType)>
            _handlers = new(StringComparer.OrdinalIgnoreCase);

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
            _poller.WaitForStart(POLLER_START_TIMEOUT);
        }

        public string BindAddress => this._router.Options.LastEndpoint ?? this._config.BindAddress;

        /// <summary>
        /// Add a handler with specified path to process request. If the path already exists, the handler will be replaced.
        /// </summary>
        /// <param name="path">Path of query without parameters.</param>
        /// <param name="handler">Handler callback to process request.</param>
        /// <returns><see langword="true"/> if added successfully. Otherwise, <see langword="false"/>.</returns>
        public bool RegisterHandler<Payload, Resp>(string path, Func<string, Payload?, Resp?> handler)
        {
            ArgumentNullException.ThrowIfNull(path);
            ArgumentNullException.ThrowIfNull(handler);
            _handlers[path] = ((p, payload) => handler(p, (Payload?)payload), typeof(Payload), typeof(Resp));
            return true;
        }

        public bool RegisterHandler(string path, ChannelHandler handler, Type? payloadType = null,
            Type? respType = null)
        {
            ArgumentNullException.ThrowIfNull(path);
            ArgumentNullException.ThrowIfNull(handler);
            _handlers[path] = (handler, payloadType, respType);
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
        public IDictionary<string, (ChannelHandler Handler, Type? PayloadType, Type? RespType)> Handlers()
        {
            return _handlers.ToImmutableDictionary();
        }

        /// <summary>
        /// Event for <see cref="_inQueue.ReceiveReady"/>
        /// </summary>
        private void InQueueReceiveReadyHandler(object? sender, NetMQQueueEventArgs<RequestInQueue> e)
        {
            while (e.Queue.TryDequeue(out var req, DEQUEUE_TIMEOUT))
            {
                ResponseOutQueue response;
                try
                {
                    var respPayload = DispatchHandler(req.Query, req.Payload);
                    response = new(req.Identity, req.RequestId, respPayload);
                }
                catch (HttpRequestException ex)
                {
                    if (_logger.IsEnabled(LogLevel.Error))
                    {
                        _logger.LogError(ex, "Error occurred while processing request {query} from socket {addr}.",
                            req.Query, _config.BindAddress);
                    }
                    var err = ErrorHandler.Handle(ex);
                    response = new(req.Identity, req.RequestId, null, err);
                }
                catch (Exception ex)
                {
                    if (_logger.IsEnabled(LogLevel.Error))
                    {
                        _logger.LogError(ex, "Error occurred while processing request {query} from socket {addr}.",
                            req.Query, _config.BindAddress);
                    }
                    var err = ErrorHandler.Handle(ex, HttpStatusCode.InternalServerError);
                    response = new(req.Identity, req.RequestId, null, err);
                }
                _outQueue.Enqueue(response);
            }
        }

        /// <summary>
        /// Event for <see cref="_outQueue.ReceiveReady"/>
        /// </summary>
        private void OutQueueReceiveReadyHandler(object? sender, NetMQQueueEventArgs<ResponseOutQueue> e)
        {
            while (e.Queue.TryDequeue(out var resp, DEQUEUE_TIMEOUT))
            {
                _router.SendMultipartMessage(CreateResponseMessage(resp.Identity, resp.RequestId, resp.Payload,
                    resp.Error));
            }
        }

        /// <summary>
        /// Event for <see cref="_router.ReceiveReady"/>.
        /// </summary>
        private void RouterReceiveReadyHandler(object? sender, NetMQSocketEventArgs e)
        {
            // router remove empty frame automatically
            // <cid> <rid> <query> [payload]
            NetMQMessage? req = new();
            if (!e.Socket.TryReceiveMultipartMessage(ref req, FRAME_COUNT) || req is null || req.IsEmpty)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Cannot get message from socket {addr}.", _config.BindAddress);
                }
                return;
            }
            var cid = req[0];
            if (req.FrameCount < 2)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Invalid message received from socket {addr}. Missing request id.",
                        _config.BindAddress);
                }
                e.Socket.SendMultipartMessage(
                    CreateResponseMessage(cid, NetMQFrame.Empty, new(ErrorHandler.BadRequestMsgPack)));
                return;
            }
            var rid = req[1];
            if (req.FrameCount is < 3)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Invalid message received from socket {addr}.", _config.BindAddress);
                }
                e.Socket.SendMultipartMessage(
                    CreateResponseMessage(cid, rid, new(ErrorHandler.BadRequestMsgPack)));
                return;
            }
            var query = _config.QueryEncoding.GetString(req[2].ToByteArray());
            var payload = req.FrameCount > 3 ? req[3] : null;
            _inQueue.Enqueue(new(cid, rid, query, payload));
        }

        /// <summary>
        /// Dispatch to matched handler.
        /// </summary>
        /// <param name="query">The request query string.</param>
        /// <param name="payload">The request payload body in msgpack format.</param>
        /// <returns>The response payload body.</returns>
        /// <exception cref="KeyNotFoundException">Path not matched. Register it firstly.</exception>
        private object? DispatchHandler(string query, NetMQFrame? payload)
        {
            var path = query.GetPath();
            if (ReservedPath.TryHandle(path, payload, out var resp))
            {
                return resp;
            }
            if (!_handlers.TryGetValue(path, out var handler))
            {
                throw new HttpRequestException("Path not found: " + path, null, HttpStatusCode.NotFound);
            }
            object? p;
            if (handler.PayloadType is null || handler.PayloadType == typeof(NetMQFrame))
            {
                p = payload;
            }
            else
            {
                p = payload?.Buffer.FromMsgPack(handler.PayloadType);
            }
            return handler.Handler(query, p);
        }

        /// <summary>
        /// Create a zmq multipart message.
        /// </summary>
        /// <remarks>{Identity} {Empty} {RequestId} {Payload} {Error}</remarks>
        private static NetMQMessage CreateResponseMessage(NetMQFrame identity, NetMQFrame requestId,
            NetMQFrame? payload = null, NetMQFrame? err = null)
        {
            var msg = new NetMQMessage(FRAME_COUNT);
            msg.Append(identity);
            msg.Append(requestId);
            msg.Append(payload ?? NetMQFrame.Empty);
            msg.Append(err ?? NetMQFrame.Empty);
            return msg;
        }

        /// <summary>
        /// Create a zmq multipart message.
        /// </summary>
        /// <remarks>{Identity} {Empty} {RequestId} {Payload} {Error}</remarks>
        private static NetMQMessage CreateResponseMessage(NetMQFrame identity, NetMQFrame requestId,
            object? payload = null, ErrorInfo? err = null)
        {
            return CreateResponseMessage(identity, requestId,
                payload switch
                {
                    null => NetMQFrame.Empty,
                    NetMQFrame pf => pf,
                    _ => new(payload.ToMsgPack())
                },
                err is null ? NetMQFrame.Empty : new(err.ToMsgPack()));
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
                this._poller.StopAsync();
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
            while (_inQueue.TryDequeue(out var req, DEQUEUE_TIMEOUT))
            {
                _router.SendMultipartMessage(CreateResponseMessage(req.Identity, req.RequestId, NetMQFrame.Empty,
                    new(ErrorHandler.ServiceUnavailableMsgPack)));
            }
        }

        private void ClearOutQueue()
        {
            while (_outQueue.TryDequeue(out var resp, DEQUEUE_TIMEOUT))
            {
                _router.SendMultipartMessage(CreateResponseMessage(resp.Identity, resp.RequestId, resp.Payload,
                    resp.Error));
            }
        }

        #endregion

        private record struct RequestInQueue(
            NetMQFrame Identity,
            NetMQFrame RequestId,
            string Query,
            NetMQFrame? Payload = null);

        private record struct ResponseOutQueue(
            NetMQFrame Identity,
            NetMQFrame RequestId,
            object? Payload = null,
            ErrorInfo? Error = null);
    }
}
