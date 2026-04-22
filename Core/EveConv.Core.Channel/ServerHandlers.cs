using System.Collections.Concurrent;
using EveConv.Channel.Client;
using EveConv.Channel.Common;
using EveConv.Channel.Server;
using EveConv.Core.Channel.Helper;
using NetMQ;

namespace EveConv.Core.Channel;

/// <summary>
/// Util to manage server handlers.
/// </summary>
internal class ServerHandlers
{
    private readonly ConcurrentDictionary<string, ClientInfo> _clientInfos;
    private readonly ChannelServer _server;

    public ServerHandlers(ConcurrentDictionary<string, ClientInfo> clientInfos, ChannelServer server)
    {
        ArgumentNullException.ThrowIfNull(clientInfos);
        ArgumentNullException.ThrowIfNull(server);
        this._clientInfos = clientInfos;
        this._server = server;
    }

    /// <summary>
    /// Register reserved commands.
    /// </summary>
    public void RegisterReservedHandlers()
    {
        this._server.RegisterHandler<SignPayload, bool>(ReservedCommand.REGISTER, RegisterClientHandler);
        this._server.RegisterHandler<SignPayload, bool>(ReservedCommand.UNREGISTER, UnregisterClientHandler);
        this._server.RegisterHandler<object, string>(ReservedCommand.PING, PingHandler);
    }

    /// <summary>
    /// Register client's export commands.
    /// </summary>
    /// <param name="clientId">The specified client id.</param>
    /// <exception cref="NotSupportedException">Client not registered.</exception>
    public void RegisterDispatchHandler(string clientId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        if (!this._clientInfos.TryGetValue(clientId, out var clientInfo))
        {
            throw new NotSupportedException($"Failed to get client info of {clientId}.");
        }
        if (clientInfo.Registered)
        {
            return;
        }
        foreach (var path in GetDispatchPaths(clientId, clientInfo.Exports))
        {
            this._server.RegisterHandler<NetMQFrame, NetMQFrame>(path, DispatchHandler);
        }
        clientInfo.Registered = true;
    }

    /// <summary>
    /// Unregister client's export commands.
    /// </summary>
    /// <param name="clientId">The specified client id.</param>
    public void UnregisterDispatchHandler(string clientId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        if (!this._clientInfos.TryGetValue(clientId, out var clientInfo) || !clientInfo.Registered)
        {
            return;
        }
        foreach (var path in GetDispatchPaths(clientId, clientInfo.Exports))
        {
            this._server.UnregisterHandler(path);
        }
        clientInfo.Registered = false;
    }

    private NetMQFrame? DispatchHandler(string path, NetMQFrame? payload)
    {
        var frag = path.Split('/', 2);
        if (frag.Length != 2)
        {
            throw new ArgumentException("Invalid path", nameof(path));
        }
        var clientId = frag[0];
        var query = frag[1];
        if (!_clientInfos.TryGetValue(clientId, out var clientInfo))
        {
            throw new KeyNotFoundException($"Client {clientId} not found.");
        }
        var serverAddr = clientInfo.ChannelServer?.BindAddress;
        if (string.IsNullOrWhiteSpace(serverAddr))
        {
            throw new NotSupportedException($"Invalid server address. " +
                                            $"Client {clientInfo.ClientId} may not registered or " +
                                            $"doesn't support receive requests.");
        }
        using var c = new ChannelClient(new() { BindAddress = serverAddr });
        // HACK: maybe send request async
        return c.SendRequest<NetMQFrame, NetMQFrame>(query, payload);
    }

    private static HashSet<string> GetDispatchPaths(string clientId, IEnumerable<string> exports)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(exports);

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var export in exports)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(export);
            paths.Add($"{clientId}/{export}");
        }

        return paths;
    }

    private bool RegisterClientHandler(string query, SignPayload? payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var signInfo = payload ?? throw new ArgumentException("Malformed payload", nameof(payload));
        return RegisterClient(signInfo.ClientId, signInfo.Content, signInfo.Sign);
    }

    private bool UnregisterClientHandler(string query, SignPayload? payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var signInfo = payload ?? throw new ArgumentException("Malformed payload", nameof(payload));
        return UnregisterClient(signInfo.ClientId, signInfo.Content, signInfo.Sign);
    }

    private string PingHandler(string _, object? __)
    {
        return "pong";
    }

    private bool RegisterClient(string clientId, ReadOnlyMemory<byte> content, ReadOnlyMemory<byte> sign)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(content.Length, nameof(content));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sign.Length, nameof(sign));
        if (!this._clientInfos.TryGetValue(clientId, out var clientInfo))
        {
            throw new KeyNotFoundException($"Client {clientId} not found");
        }
        if (!RegisterValid.RsaVerifySign(content, sign,
                clientInfo.SignInfo.Pem,
                clientInfo.SignInfo.HashAlgorithm,
                clientInfo.SignInfo.PaddingMode))
        {
            return false;
        }
        RegisterDispatchHandler(clientId);
        return true;
    }

    private bool UnregisterClient(string clientId, ReadOnlyMemory<byte> content, ReadOnlyMemory<byte> sign)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(content.Length, nameof(content));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sign.Length, nameof(sign));
        if (!this._clientInfos.TryGetValue(clientId, out var clientInfo))
        {
            return true;
        }
        if (!RegisterValid.RsaVerifySign(content, sign,
                clientInfo.SignInfo.Pem,
                clientInfo.SignInfo.HashAlgorithm,
                clientInfo.SignInfo.PaddingMode))
        {
            return false;
        }
        UnregisterDispatchHandler(clientId);
        return true;
    }
}

public class SignPayload
{
    public string ClientId { get; init; } = string.Empty;
    public ReadOnlyMemory<byte> Content { get; init; } = ReadOnlyMemory<byte>.Empty;
    public ReadOnlyMemory<byte> Sign { get; init; } = ReadOnlyMemory<byte>.Empty;
}
