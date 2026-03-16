using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Channel.Common
{
    public static class UtilExtensions
    {
        private static readonly MessagePack.MessagePackSerializerOptions MSGPACK_SER_OPTIONS =
            MessagePack.Resolvers.ContractlessStandardResolver.Options.WithCompression(
                MessagePack.MessagePackCompression.Lz4BlockArray);

        extension(ReadOnlyMemory<byte>? data)
        {
            public T? FromMsgPack<T>()
            {
                return (T?)data.FromMsgPack(typeof(T));
            }

            public object? FromMsgPack(Type type)
            {
                return FromMsgPack(type, data);
            }
        }
        
        extension(byte[]? data)
        {
            public T? FromMsgPack<T>()
            {
                return (T?)data.FromMsgPack(typeof(T));
            }

            public object? FromMsgPack(Type type)
            {
                return FromMsgPack(type, data);
            }
        }
        

        private static object? FromMsgPack(Type type, ReadOnlyMemory<byte>? data)
        {
            if (!data.HasValue || data.Value.Length == 0)
            {
                return null;
            }
            return MessagePack.MessagePackSerializer.Deserialize(type, data.Value, MSGPACK_SER_OPTIONS);
        }

        public static byte[] ToMsgPack(this object? obj)
        {
            return MessagePack.MessagePackSerializer.Serialize(obj, MSGPACK_SER_OPTIONS);
        }
    }
}
