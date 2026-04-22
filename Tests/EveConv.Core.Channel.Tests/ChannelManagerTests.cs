using System.Net;
using System.Security.Cryptography;
using EveConv.Channel.Client;
using EveConv.Channel.Common;
using EveConv.Channel.Server;

namespace EveConv.Core.Channel.Tests;

public class ChannelManagerTests
{
    [Fact]
    public void BindAddress_WithEphemeralPort_ReturnsResolvedAddress()
    {
        using var manager = new ChannelManager(new ChannelConfig { BindAddress = "tcp://localhost:0" });
        Assert.StartsWith("tcp://127.0.0.1:", manager.BindAddress);
        Assert.False(manager.BindAddress.EndsWith(":0", StringComparison.Ordinal));
    }

    [Fact]
    public void RegisterRequest_ThenRemoveClient_DispatchesUntilClientRemoved()
    {
        using var signingKey = RSA.Create(2048);
        using var targetServer = new ChannelServer(new ChannelServerConfig { BindAddress = "tcp://localhost:0" });
        targetServer.RegisterHandler<string, string>("echo", (_, payload) => payload);

        using var manager = new ChannelManager(new ChannelConfig { BindAddress = "tcp://localhost:0" });
        manager.AddClient(new ClientInfo
        {
            ClientId = "worker",
            Exports = ["echo"],
            ChannelServer = targetServer,
            SignInfo = new ClientSignInfo { Pem = signingKey.ExportRSAPublicKeyPem() },
        });
        using var client = new ChannelClient(new ChannelClientConfig { BindAddress = manager.BindAddress });
        var registerPayload = CreateSignedPayload("worker", "register-worker"u8.ToArray(), signingKey);

        var registered = client.SendRequest<SignPayload, bool>(ReservedCommand.REGISTER, registerPayload);
        var forwarded = client.SendRequest<string, string>("worker/echo", "hello");
        var removed = manager.RemoveClient("worker");

        Assert.True(registered);
        Assert.Equal("hello", forwarded);
        Assert.NotNull(removed);
        Assert.Equal("worker", removed.ClientId);

        var exception = Assert.Throws<HttpRequestException>(() =>
            client.SendRequest<string, string>("worker/echo", "hello"));
        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
    }

    [Fact]
    public void RegisterRequest_WithInvalidSignature_DoesNotExposeClientHandlers()
    {
        using var trustedKey = RSA.Create(2048);
        using var attackerKey = RSA.Create(2048);
        using var targetServer = new ChannelServer(new ChannelServerConfig { BindAddress = "tcp://localhost:0" });
        _ = targetServer.RegisterHandler<string, string>("echo", (_, payload) => payload);

        using var manager = new ChannelManager(new ChannelConfig { BindAddress = "tcp://localhost:0" });
        manager.AddClient(new ClientInfo
        {
            ClientId = "worker",
            Exports = ["echo"],
            ChannelServer = targetServer,
            SignInfo = new ClientSignInfo { Pem = trustedKey.ExportRSAPublicKeyPem() },
        });

        using var client = new ChannelClient(new ChannelClientConfig { BindAddress = manager.BindAddress });
        var registerPayload = CreateSignedPayload("worker", "register-worker"u8.ToArray(), attackerKey);

        var registered =
            client.SendRequest<SignPayload, bool>(ReservedCommand.REGISTER, registerPayload, TimeSpan.FromSeconds(1));

        Assert.False(registered);

        var exception = Assert.Throws<HttpRequestException>(() =>
            client.SendRequest<string, string>("worker/echo", "hello"));
        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
    }

    [Fact]
    public void RegisterRequest_HandleRaiseError_ErrorInfo()
    {
        using var signingKey = RSA.Create(2048);
        using var targetServer = new ChannelServer(new ChannelServerConfig { BindAddress = "tcp://localhost:0" });
        targetServer.RegisterHandler<string, string>("echo", (_, payload) => throw new Exception("ERR"));

        using var manager = new ChannelManager(new ChannelConfig { BindAddress = "tcp://localhost:0" });
        manager.AddClient(new ClientInfo
        {
            ClientId = "worker",
            Exports = ["echo"],
            ChannelServer = targetServer,
            SignInfo = new ClientSignInfo { Pem = signingKey.ExportRSAPublicKeyPem() },
        });
        using var client = new ChannelClient(new ChannelClientConfig { BindAddress = manager.BindAddress });
        var registerPayload = CreateSignedPayload("worker", "register-worker"u8.ToArray(), signingKey);

        var registered = client.SendRequest<SignPayload, bool>(ReservedCommand.REGISTER, registerPayload);
        var err = Assert.Throws<HttpRequestException>(() => client.SendRequest<string, string>("worker/echo", "hello"));
        Assert.Equal("ERR", err.Message);
        Assert.Equal(HttpStatusCode.InternalServerError, err.StatusCode);
    }

    private static SignPayload CreateSignedPayload(string clientId, byte[] content, RSA rsa)
    {
        return new()
        {
            ClientId = clientId,
            Content = content,
            Sign = rsa.SignData(content, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
        };
    }
}
