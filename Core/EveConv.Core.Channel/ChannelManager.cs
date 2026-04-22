using System.Collections.Concurrent;
using EveConv.Channel.Server;

namespace EveConv.Core.Channel
{
    public class ChannelManager : IDisposable
    {
        private bool _disposed = false;

        /// <summary>
        /// Registered client infos.
        /// </summary>
        private readonly ConcurrentDictionary<string, ClientInfo> _clientInfos = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Channel server to receive request from registered <see cref="ClientInfo.ChannelServer"/>.
        /// </summary>
        /// <remarks>
        ///     As a message bus to hide actual <see cref="ClientInfo.ChannelClient"/>.
        ///     Got request and dispatch to specified <see cref="ClientInfo.ChannelClient"/>.
        /// </remarks>
        private readonly ChannelServer _server;

        /// <summary>
        /// Gets the actual bound address of this manager server.
        /// </summary>
        public string BindAddress => this._server.BindAddress;

        /// <summary>
        /// Handlers to dispatch requests.
        /// </summary>
        private readonly ServerHandlers _handlers;

        public ChannelManager(ChannelConfig config, IDictionary<string, ClientInfo>? clientInfos = null)
        {
            ArgumentNullException.ThrowIfNull(config);
            this._server = new(config);
            if (clientInfos is not null)
            {
                foreach (var clientInfo in clientInfos.Values)
                {
                    ValidateClientInfo(clientInfo);
                }
                this._clientInfos = new(clientInfos, StringComparer.OrdinalIgnoreCase);
            }
            this._handlers = new(_clientInfos, _server);
            _handlers.RegisterReservedHandlers();
        }

        /// <summary>
        /// Add a client to manager.
        /// </summary>
        /// <param name="clientInfo">Client info.</param>
        public void AddClient(ClientInfo clientInfo)
        {
            ThrowIfDisposed();
            ValidateClientInfo(clientInfo);
            if (this._clientInfos.TryAdd(clientInfo.ClientId, clientInfo))
            {
                return;
            }
            throw new ArgumentException($"Client {clientInfo.ClientId} already exists. Please remove it firstly.");
        }

        /// <summary>
        /// Remove this client from manager.
        /// </summary>
        /// <param name="clientInfo">Client info</param>
        /// <returns>Removed client info if exists.</returns>
        public ClientInfo? RemoveClient(ClientInfo clientInfo)
        {
            ArgumentNullException.ThrowIfNull(clientInfo);
            return RemoveClient(clientInfo.ClientId);
        }

        /// <summary>
        /// Remove this client from manager.
        /// </summary>
        /// <param name="clientId">Client ID</param>
        /// <returns>Removed client info if exists.</returns>
        public ClientInfo? RemoveClient(string clientId)
        {
            ThrowIfDisposed();
            ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
            this._handlers.UnregisterDispatchHandler(clientId);
            return this._clientInfos.TryRemove(clientId, out var clientInfo) ? clientInfo : null;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            this._server.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ChannelManager));
            }
        }

        private static void ValidateClientInfo(ClientInfo clientInfo)
        {
            ArgumentNullException.ThrowIfNull(clientInfo);
            ArgumentException.ThrowIfNullOrWhiteSpace(clientInfo.ClientId);
            ArgumentNullException.ThrowIfNull(clientInfo.Exports);
            if (clientInfo.Exports.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("Client exports cannot contain empty command names.",
                    nameof(clientInfo));
            }
        }
    }
}
