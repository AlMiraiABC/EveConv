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
            server.RegisterHandler(path, handler);
            Assert.Contains(path, server.Handlers());
            return;

            object? handler(string p, ReadOnlyMemory<byte>? b) => "TEST";
        }

        [Fact]
        public void RegisterHandler_Override_Success()
        {
            const string path = "/test/handler";
            server.RegisterHandler(path, handler1);
            server.RegisterHandler(path, handler2);
            Assert.Equal(server.Handlers()[path], handler2);
            return;

            object? handler1(string p, ReadOnlyMemory<byte>? b) => "TEST1";
            object? handler2(string p, ReadOnlyMemory<byte>? b) => "TEST2";
        }

        [Fact]
        public void UnregisterHandler_Exists_Success()
        {
            const string path = "/test/handler";
            server.RegisterHandler(path, (p, b) => "TEST");
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
            using var req = new RequestSocket(address);
            req.SendMoreFrame(rid).SendFrame(path);
            var resp = req.ReceiveMultipartMessage();
            Assert.Equal(3, resp.FrameCount);
            Assert.Equal(rid, resp[0].ConvertToString(Encoding.UTF8));
            Assert.Equal("pong", Deserialize<string>(resp[1].Buffer)); // body
            Assert.True(resp[2].IsEmpty); // error
        }

        [Fact]
        public void Handle_Path_Success()
        {
            const string path = "/test/handler";
            server.RegisterHandler(path,
                (p, b) => Deserialize<string>(b!.Value) + " World");
            var rid = Guid.NewGuid().ToString();
            using var req = new RequestSocket(address);
            req.SendMoreFrame(rid).SendMoreFrame(path).SendFrame(Serialize("Hello"));
            var resp = req.ReceiveMultipartMessage();
            Assert.Equal(3, resp.FrameCount);
            Assert.Equal(rid, resp[0].ConvertToString(Encoding.UTF8));
            Assert.Equal("Hello World", Deserialize<string>(resp[1].Buffer));
            Assert.True(resp[2].IsEmpty);
        }

        [Fact]
        public void Handle_Throw_Failed()
        {
            const string path = "/test/handler";
            server.RegisterHandler(path, (p, b) => throw new NotSupportedException(p));
            var rid = Guid.NewGuid().ToString();
            using var req = new RequestSocket(address);
            req.SendMoreFrame(rid).SendFrame(path);
            var resp = req.ReceiveMultipartMessage();
            Assert.Equal(3, resp.FrameCount);
            Assert.Equal(rid, resp[0].ConvertToString(Encoding.UTF8));
            Assert.True(resp[1].IsEmpty);
            var actual = Deserialize<ErrorInfo>(resp[2].Buffer);
            Assert.NotNull(actual);
            Assert.Equal((int)HttpStatusCode.InternalServerError, actual.ErrorCode);
            Assert.Equal(path, actual.Message);
        }

        [Fact]
        public void Handle_Query_Success()
        {
            const string path = "/test/query";
            const string param = "name=test&value=123";
            server.RegisterHandler(path, (query, b) =>
            {
                var queryPart = query.Contains('?') ? query[(query.IndexOf('?') + 1)..] : "";
                return queryPart;
            });
            var rid = Guid.NewGuid().ToString();
            using var req = new RequestSocket(address);
            req.SendMoreFrame(rid).SendFrame($"{path}?{param}");
            var resp = req.ReceiveMultipartMessage();
            Assert.Equal(3, resp.FrameCount);
            Assert.Equal(rid, resp[0].ConvertToString(Encoding.UTF8));
            Assert.Equal(param, Deserialize<string>(resp[1].Buffer)); // body contains the query parameters
            Assert.True(resp[2].IsEmpty); // no error
        }

        private static readonly MessagePack.MessagePackSerializerOptions MSGPACK_SER_OPTIONS =
            MessagePack.Resolvers.ContractlessStandardResolver.Options.WithCompression(MessagePack
                .MessagePackCompression.Lz4BlockArray);

        private static byte[] Serialize(object? obj)
        {
            return MessagePack.MessagePackSerializer.Serialize(obj, MSGPACK_SER_OPTIONS);
        }

        private static T? Deserialize<T>(ReadOnlyMemory<byte> bytes)
        {
            return MessagePack.MessagePackSerializer.Deserialize<T?>(bytes, MSGPACK_SER_OPTIONS);
        }
    }
}
