using System.Net;
using System.Text;
using EveConv.Channel.Common;
using NetMQ;
using NetMQ.Sockets;

namespace EveConv.Channel.Client.Tests;

public class ChannelClientTests
{
    [Fact]
    public async Task SendRequestAsync_WithResponse_Success()
    {
        var server = new RouterSocket("tcp://localhost:0");
        var address = server.Options.LastEndpoint ?? throw new NullReferenceException("Cannot get endpoint");
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(1));
        string? resp;
        try
        {
            _ = RunServer(server, "World", cancellationToken: cancellationTokenSource.Token);
            using var client = new ChannelClient(new() { BindAddress = $">{address}" });
            resp = await client.SendRequestAsync<string, string>("/test", "Hello",
                cancellationToken: cancellationTokenSource.Token);
        }
        finally
        {
            await cancellationTokenSource.CancelAsync();
            server.Dispose();
        }
        Assert.NotNull(resp);
        Assert.Equal("World", resp);
    }

    [Fact]
    public async Task SendRequestAsync_NoResponse_Success()
    {
        var server = new RouterSocket("tcp://localhost:0");
        var address = server.Options.LastEndpoint ?? throw new NullReferenceException("Cannot get endpoint");
        var cancellationTokenSource = new CancellationTokenSource();
        string? resp;
        try
        {
            _ = RunServer(server, cancellationToken: cancellationTokenSource.Token);
            using var client = new ChannelClient(new() { BindAddress = address });
            resp = await client.SendRequestAsync<string, string>("/test", "Hello",
                cancellationToken: cancellationTokenSource.Token);
        }
        finally
        {
            await cancellationTokenSource.CancelAsync();
            server.Dispose();
        }
        Assert.True(resp is null);
    }

    [Fact]
    public async Task SendRequestAsync_ResponseThrow_Success()
    {
        var server = new RouterSocket("tcp://localhost:0");
        var address = server.Options.LastEndpoint ?? throw new NullReferenceException("Cannot get endpoint");
        var cancellationTokenSource = new CancellationTokenSource();
        try
        {
            _ = RunServer(server, "World", cancellationToken: cancellationTokenSource.Token);
            using var client = new ChannelClient(new() { BindAddress = address });
            await Assert.ThrowsAsync<MessagePack.MessagePackSerializationException>(() =>
                client.SendRequestAsync<string, int>("/test", "Hello",
                    cancellationToken: cancellationTokenSource.Token));
        }
        finally
        {
            await cancellationTokenSource.CancelAsync();
            server.Dispose();
        }
    }

    [Fact]
    public async Task SendRequestAsync_WithError_Success()
    {
        var server = new RouterSocket("tcp://localhost:0");
        var address = server.Options.LastEndpoint ?? throw new NullReferenceException("Cannot get endpoint");
        var cancellationTokenSource = new CancellationTokenSource();
        try
        {
            _ = RunServer(server, null, new ErrorInfo("Err", 500), cancellationToken: cancellationTokenSource.Token);
            using var client = new ChannelClient(new() { BindAddress = address });
            await Assert.ThrowsAnyAsync<HttpRequestException>(() => client.SendRequestAsync<string, string>("/test",
                    "Hello",
                    cancellationToken: cancellationTokenSource.Token),
                (ex) =>
                {
                    if (ex.Message != "Err")
                    {
                        return "Message: " + ex.Message;
                    }
                    if (ex.StatusCode != HttpStatusCode.InternalServerError)
                    {
                        return "StatusCode: " + ex.StatusCode;
                    }
                    return null;
                });
        }
        finally
        {
            await cancellationTokenSource.CancelAsync();
            server.Dispose();
        }
    }

    [Fact]
    public async Task SendRequestAsync_ErrorThrow_Success()
    {
        var server = new RouterSocket("tcp://localhost:0");
        var address = server.Options.LastEndpoint ?? throw new NullReferenceException("Cannot get endpoint");
        var cancellationTokenSource = new CancellationTokenSource();
        try
        {
            _ = RunServer(server, null, 123, cancellationToken: cancellationTokenSource.Token);
            using var client = new ChannelClient(new() { BindAddress = address });
            await Assert.ThrowsAnyAsync<MessagePack.MessagePackSerializationException>(() =>
                    client.SendRequestAsync<string, string>("/test",
                        "Hello",
                        cancellationToken: cancellationTokenSource.Token),
                (ex) =>
                {
                    if (!ex.Message.Contains(nameof(ErrorInfo)))
                    {
                        return "Message: " + ex.Message;
                    }
                    return null;
                });
        }
        finally
        {
            await cancellationTokenSource.CancelAsync();
            server.Dispose();
        }
    }

    /// <summary>
    /// Mock router socket server logic to response
    /// </summary>
    private static Task RunServer(RouterSocket server,
        object? result = null,
        object? err = null,
        string query = "/test",
        string? payload = "Hello",
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var msg = server.ReceiveMultipartMessage(4);
            Assert.Equal(4, msg.FrameCount);
            var identity = msg[0];
            var reqId = msg[1];
            Assert.Equal(query, msg[2].ConvertToString(Encoding.UTF8));
            if (payload is null)
            {
                if (!msg[3].IsEmpty)
                {
                    result = null;
                    err = new ErrorInfo($"Payload should be null, bug got length {msg[2].BufferSize}.", 500);
                }
            }
            else
            {
                var actual = msg[3].Buffer.FromMsgPack<string>();
                if (payload != actual)
                {
                    result = null;
                    err = new ErrorInfo($"Payload should be {payload}, but got {actual}.", 500);
                }
            }
            // <cid> <empty> <rid> <result> <err>
            var send = new NetMQMessage();
            send.Append(identity);
            send.Append(reqId);
            send.Append(result is null ? NetMQFrame.Empty : new NetMQFrame(result.ToMsgPack()));
            send.Append(err is null ? NetMQFrame.Empty : new NetMQFrame(err.ToMsgPack()));
            server.SendMultipartMessage(send);
        }, cancellationToken);
    }
}
