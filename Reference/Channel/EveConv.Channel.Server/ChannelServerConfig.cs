using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Channel.Server
{
    public class ChannelServerConfig
    {
        /// <summary>
        /// Accept the connection address.
        /// </summary>
        /// <remarks>IPC is not supported(NetMQ).</remarks>
        public string BindAddress { get; set; } = "tcp://localhost:0";
        /// <summary>
        /// Size of request queue. Default to <c>0</c> for unlimited.
        /// </summary>
        public int QueueSize { get; set; } = 0;
        /// <summary>
        /// Encoding for request query string.
        /// </summary>
        public Encoding QueryEncoding { get; set; } = Encoding.UTF8;

        internal void Valid()
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(BindAddress);
            ArgumentNullException.ThrowIfNull(QueryEncoding);
            ArgumentOutOfRangeException.ThrowIfLessThan(QueueSize, 0);
        }
    }
}
