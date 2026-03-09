using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Channel.Client
{
    public static class UtilExtensions
    {
        private static readonly MessagePack.MessagePackSerializerOptions MSGPACK_SER_OPTIONS =
            MessagePack.Resolvers.ContractlessStandardResolver.Options.WithCompression(
                MessagePack.MessagePackCompression.Lz4BlockArray);

        extension(byte[]? data)
        {
            public T? FromMsgPack<T>()
            {
                return (T?)data.FromMsgPack(typeof(T));
            }

            public object? FromMsgPack(Type type)
            {
                if (data is null || data.Length == 0)
                {
                    return null;
                }
                return MessagePack.MessagePackSerializer.Deserialize(type, data, MSGPACK_SER_OPTIONS);
            }
        }

        public static byte[] ToMsgPack(this object? obj)
        {
            return MessagePack.MessagePackSerializer.Serialize(obj, MSGPACK_SER_OPTIONS);
        }
    }
}
