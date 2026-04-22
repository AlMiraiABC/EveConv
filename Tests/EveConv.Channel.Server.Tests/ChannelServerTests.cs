using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using EveConv.Channel.Common;
using EveConv.Channel.Server;
using NetMQ;
using NetMQ.Sockets;

namespace EveConv.Channel.Server.Tests
{
    public class ChannelServerTests
    {
        private readonly ChannelServer server;
        private readonly string address;

        public ChannelServerTests()
        {
            server = new(new());
            address = server.BindAddress;
        }

        [Fact]
        public void RegisterHandler_Unexists_Success()
        {
            const string path = "/test/handler";
            server.RegisterHandler<string, string>(path, handler);
            Assert.Contains(path, server.Handlers());
            return;

            string? handler(string p, string? b) => "TEST";
        }

        [Fact]
        public void RegisterHandler_Override_Success()
        {
            const string path = "/test/handler";
            server.RegisterHandler<string, string>(path, handler1);
            server.RegisterHandler<string, int?>(path, handler2);
            Assert.Equal(typeof(int?), server.Handlers()[path].RespType);
            return;

            string? handler1(string p, string? b) => "TEST1";
            int? handler2(string p, string? b) => 1;
        }

        [Fact]
        public void UnregisterHandler_Exists_Success()
        {
            const string path = "/test/handler";
            server.RegisterHandler(path, (_, _) => "TEST");
            var r = server.UnregisterHandler(path);
            Assert.True(r);
            Assert.DoesNotContain(path, server.Handlers());
        }

        [Fact]
        public void UnregisterHandler_Unexists_Success()
        {
            const string path = "/test/handler";
            var r = server.UnregisterHandler(path);
            Assert.False(r);
            Assert.DoesNotContain(path, server.Handlers());
        }

        [Fact]
        public void Handle_Ping_Success()
        {
            const string path = "ping";
            var rid = Guid.NewGuid().ToString();
            using var client = new DealerSocket(address);
            client.SendMoreFrame(rid).SendFrame(path);
            var resp = client.ReceiveMultipartMessage(4);
            Assert.Equal(3, resp.FrameCount);
            Assert.Equal(rid, resp[0].ConvertToString(Encoding.UTF8));
            Assert.Equal("pong", resp[1].Buffer.FromMsgPack<string>()); // body
            Assert.True(resp[2].IsEmpty); // error
        }

        [Fact]
        public void Handle_Path_Success()
        {
            const string path = "/test/handler";
            server.RegisterHandler<string, string>(path,
                (p, b) => b + " World");
            var rid = Guid.NewGuid().ToString();
            using var client = new DealerSocket(address);
            client.SendMoreFrame(rid).SendMoreFrame(path).SendFrame("Hello".ToMsgPack());
            var resp = client.ReceiveMultipartMessage(3);
            Assert.Equal(3, resp.FrameCount);
            Assert.Equal(rid, resp[0].ConvertToString(Encoding.UTF8));
            Assert.Equal("Hello World", resp[1].Buffer.FromMsgPack<string>());
            Assert.True(resp[2].IsEmpty);
        }

        [Fact]
        public void Handle_Throw_Failed()
        {
            const string path = "/test/handler";
            server.RegisterHandler(path, (p, _) => throw new NotSupportedException(p));
            var rid = Guid.NewGuid().ToString();
            using var client = new DealerSocket(address);
            client.SendMoreFrame(rid).SendFrame(path);
            var resp = client.ReceiveMultipartMessage(3);
            Assert.Equal(3, resp.FrameCount);
            Assert.Equal(rid, resp[0].ConvertToString(Encoding.UTF8));
            Assert.True(resp[1].IsEmpty);
            var actual = resp[2].Buffer.FromMsgPack<ErrorInfo>();
            Assert.NotNull(actual);
            Assert.Equal((int)HttpStatusCode.InternalServerError, actual.ErrorCode);
            Assert.Equal(path, actual.Message);
        }

        [Fact]
        public void Handle_Query_Success()
        {
            const string path = "/test/query";
            const string param = "name=test&value=123";
            server.RegisterHandler(path, (query, _) =>
            {
                var queryPart = query.Contains('?') ? query[(query.IndexOf('?') + 1)..] : "";
                return queryPart;
            });
            var rid = Guid.NewGuid().ToString();
            using var client = new DealerSocket(address);
            client.SendMoreFrame(rid).SendFrame($"{path}?{param}");
            var resp = client.ReceiveMultipartMessage(3);
            Assert.Equal(3, resp.FrameCount);
            Assert.Equal(rid, resp[0].ConvertToString(Encoding.UTF8));
            Assert.Equal(param, resp[1].Buffer.FromMsgPack<string>()); // body contains the query parameters
            Assert.True(resp[2].IsEmpty); // no error
        }
    }
}
