using System.Security.Cryptography;
using EveConv.Channel.Client;
using EveConv.Channel.Server;

namespace EveConv.Core.Channel;

/// <summary>
/// Client base info.
/// </summary>
public class ClientInfo
{
    /// <summary>
    /// Unique client id.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Export method names.
    /// </summary>
    public List<string> Exports { get; set; } = [];

    public ClientSignInfo SignInfo { get; set; } = new();
    public ChannelClientConfig ChannelConfig { get; set; } = new();

    /// <summary>
    /// Channel client to send request to other servers.
    /// </summary>
    public ChannelClient? ChannelClient { get; init; }

    /// <summary>
    /// Channel server to receive request from other clients.
    /// </summary>
    public ChannelServer? ChannelServer { get; init; }

    /// <summary>
    /// Determine whether this client is registered.
    /// </summary>
    internal bool Registered { get; set; } = false;
}

/// <summary>
/// Client signature algorithm info.
/// </summary>
public class ClientSignInfo
{
    public string Pem { get; set; } = string.Empty;
    public string HashAlgorithm { get; set; } = "SHA256";
    public RSASignaturePaddingMode PaddingMode { get; set; } = RSASignaturePaddingMode.Pkcs1;
}
