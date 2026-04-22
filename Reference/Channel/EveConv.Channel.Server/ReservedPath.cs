using System;
using System.Collections.Generic;
using System.Text;
using NetMQ;

namespace EveConv.Channel.Server
{
    internal static class ReservedPath
    {
        private static readonly Dictionary<string, ChannelHandler> RESERVED_HANDLER = new(StringComparer.OrdinalIgnoreCase)
        {
            {"ping", Ping },
        };

        private static object Ping(string _, object? __) => "pong";

        public static bool TryHandle(string path, object? payload, out object? response)
        {
            if (!RESERVED_HANDLER.TryGetValue(path, out var h))
            {
                response = string.Empty;
                return false;
            }
            response = h(path, payload);
            return true;
        }
    }
}
