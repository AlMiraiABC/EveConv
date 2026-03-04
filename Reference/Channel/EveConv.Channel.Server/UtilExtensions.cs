using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Channel.Server
{
    internal static class UtilExtensions
    {
        private static readonly MessagePack.MessagePackSerializerOptions MSGPACK_SER_OPTIONS =
            MessagePack.Resolvers.ContractlessStandardResolver.Options.WithCompression(
                MessagePack.MessagePackCompression.Lz4BlockArray);


        public static string GetPath(this string query)
        {
            var idx = query.IndexOf('?');
            return idx < 0 ? query : query[..idx];
        }

        public static byte[] ToMsgPack(this object? obj)
        {
            return MessagePack.MessagePackSerializer.Serialize(obj, MSGPACK_SER_OPTIONS);
        }
    }
}
