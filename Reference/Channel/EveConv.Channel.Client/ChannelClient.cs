using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using EveConv.Channel.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NetMQ;
using NetMQ.Sockets;

namespace EveConv.Channel.Client;

public class ChannelClient : IDisposable
{
    private readonly ChannelClientConfig _config;
    private readonly ILogger<ChannelClient> _logger;

    private bool _disposed = false;

    private const int FRAME_COUNT = 5;
    private static readonly TimeSpan DEQUEUE_TIMEOUT = TimeSpan.Zero;

    private readonly byte[] ClientId = Guid.NewGuid().ToByteArray();
    private readonly ConcurrentDictionary<string, ResponseCallback> _sending = new(StringComparer.OrdinalIgnoreCase);
    private readonly NetMQQueue<(Request, ResponseCallback)> _requestQueue;

    private readonly DealerSocket _dealer;
    private readonly NetMQPoller _poller;

    public ChannelClient(ChannelClientConfig config, ILogger<ChannelClient>? logger = null)
    {
        config.Valid();
        this._config = config;
        this._logger = logger ?? NullLogger<ChannelClient>.Instance;
        if (_config.WaitQueueSize <= 0)
        {
            _requestQueue = new();
        }
        else
        {
            _requestQueue = new(_config.WaitQueueSize);
        }
        _requestQueue.ReceiveReady += RequestQueueReceiveReady;
        _dealer = new(config.BindAddress);
        _dealer.Options.Identity = ClientId;
        _dealer.ReceiveReady += DealerReceiveReady;
        _poller = [_dealer, _requestQueue];
        _poller.RunAsync();
    }

    private void DealerReceiveReady(object? sender, NetMQSocketEventArgs e)
    {
        // <cid> <empty> <rid> <payload> [err]
        NetMQMessage? resp = new();
        while (e.Socket.TryReceiveMultipartMessage(DEQUEUE_TIMEOUT, ref resp, FRAME_COUNT))
        {
            if (resp is null || resp.IsEmpty)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Cannot get message from socket {addr}.", _config.BindAddress);
                }
                return;
            }
            // var cid = resp[0];
            if (resp.FrameCount < 3)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Invalid message received from socket {addr}. Missing request id. Drop it.",
                        _config.BindAddress);
                }
                return;
            }
            var rid = resp[2].ConvertToString();
            if (!_sending.TryGetValue(rid, out var callback))
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Unknown response {rid} from socket {addr}. Drop it.", rid, _config.BindAddress);
                }
                return;
            }
            if (resp.FrameCount < 4)
            {
                InvokeOnSuccess(rid, null, callback);
                return;
            }

            #region response

            var payload = resp[3];
            object? response;
            try
            {
                response = payload.IsEmpty ? null : payload.Buffer.FromMsgPack(callback.ResponseType);
            }
            catch (Exception ex)
            {
                InvokeOnError(rid, ex, callback);
                return;
            }
            if (response is not null || resp.FrameCount < FRAME_COUNT)
            {
                InvokeOnSuccess(rid, response, callback);
                return;
            }

            #endregion

            #region err

            ErrorInfo? err;
            try
            {
                err = resp[4].IsEmpty ? null : resp[4].Buffer.FromMsgPack<ErrorInfo>();
            }
            catch (Exception ex)
            {
                InvokeOnError(rid, ex, callback);
                return;
            }

            #endregion

            if (err is null)
            {
                InvokeOnSuccess(rid, null, callback);
                return;
            }
            InvokeOnError(rid, new HttpRequestException(err.Message, null, (HttpStatusCode)err.ErrorCode), callback);
        }
    }

    private void InvokeOnSuccess(string rid, object? result, ResponseCallback callback)
    {
        try
        {
            callback.OnSuccess?.Invoke(result);
        }
        catch (Exception ex)
        {
            InvokeOnError(rid, ex, callback);
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(ex, "Failed to callback when success for request {rid}", rid);
            }
        }
        finally
        {
            _sending.TryRemove(rid, out _);
        }
    }

    private void InvokeOnError(string rid, Exception ex, ResponseCallback callback)
    {
        try
        {
            callback.OnError?.Invoke(ex);
        }
        catch (Exception exc)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(exc, "Failed to callback when error for request {rid}", rid);
            }
        }
        finally
        {
            _sending.TryRemove(rid, out _);
        }
    }

    private void RequestQueueReceiveReady(object? sender, NetMQQueueEventArgs<(Request, ResponseCallback)> e)
    {
        while (_requestQueue.TryDequeue(out var req, DEQUEUE_TIMEOUT))
        {
            var rid = Guid.NewGuid().ToString("N");
            try
            {
                var msg = new NetMQMessage();
                msg.Append(ClientId);
                msg.AppendEmptyFrame();
                msg.Append(rid);
                msg.Append(req.Item1.Query);
                if (req.Item1.Payload is null)
                {
                    msg.AppendEmptyFrame();
                }
                else
                {
                    msg.Append(req.Item1.Payload.ToMsgPack());
                }
                _sending[rid] = req.Item2;
                _dealer.SendMultipartMessage(msg);
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("Send request {rid} of query {query}", rid, req.Item1.Query);
                }
            }
            catch (Exception ex)
            {
                req.Item2.OnError?.Invoke(ex);
                _sending.TryRemove(rid, out _);
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Failed to send request {rid} of query {query}", rid, req.Item1.Query);
                }
            }
        }
    }

    public async Task<Resp?> SendRequestAsync<Req, Resp>(string query, Req? request, TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _requestQueue.Enqueue((new(query, request),
            new(typeof(Resp), OnSuccess, OnError)));
        try
        {
            var task = timeout.HasValue
                ? tcs.Task.WaitAsync(timeout.Value, cancellationToken)
                : tcs.Task.WaitAsync(cancellationToken);
            return (Resp?)await task;
        }
        catch (OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException($"Request '{query}' timed out");
        }
        void OnSuccess(object? result) => tcs.TrySetResult(result);
        void OnError(Exception ex) => tcs.TrySetException(ex);
    }

    public Resp? SendRequest<Req, Resp>(string query, Req? request, TimeSpan? timeout = null)
    {
        var ev = new AutoResetEvent(false);
        var sucSig = false;
        object? result = null;
        var errSig = false;
        Exception? error = null;
        _requestQueue.Enqueue((new(query, request),
            new(typeof(Resp), OnSuccess, OnError)));
        if (timeout.HasValue)
        {
            ev.WaitOne(timeout.Value);
        }
        else
        {
            ev.WaitOne();
        }
        if (sucSig)
        {
            return (Resp?)result;
        }
        if (errSig)
        {
            throw error!;
        }
        throw new UnreachableException(); // shouldn't happen

        void OnSuccess(object? req)
        {
            ev.Set();
            sucSig = true;
            result = req;
        }

        void OnError(Exception ex)
        {
            ev.Set();
            errSig = true;
            error = ex;
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
        _requestQueue.ReceiveReady -= RequestQueueReceiveReady;
        if (!_requestQueue.IsDisposed)
        {
            _requestQueue.Dispose();
        }
        _dealer.ReceiveReady -= DealerReceiveReady;
        if (!_dealer.IsDisposed)
        {
            _dealer.Dispose();
        }
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

internal record Request(string Query, object? Payload);

public class ResponseCallback
{
    public ResponseCallback(Type responseType)
    {
        this.ResponseType = responseType;
    }

    public ResponseCallback(Type responseType, Action<object?>? onSuccess)
    {
        this.ResponseType = responseType;
        this.OnSuccess = onSuccess;
    }

    public ResponseCallback(Type responseType, Action<Exception>? onError)
    {
        this.ResponseType = responseType;
        this.OnError = onError;
    }

    public ResponseCallback(Type responseType, Action<object?>? onSuccess, Action<Exception>? onError)
    {
        this.ResponseType = responseType;
        this.OnSuccess = onSuccess;
        this.OnError = onError;
    }

    public Type ResponseType { get; init; }
    public Action<object?>? OnSuccess { get; init; }
    public Action<Exception>? OnError { get; init; }
}
