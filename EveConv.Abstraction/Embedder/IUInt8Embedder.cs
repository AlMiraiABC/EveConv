using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.Embedder
{
    /// <summary>
    /// Generic interface for embedding models that using 8-bit unsigned integer.
    /// </summary>
    /// <typeparam name="I">Type of input.</typeparam>
    public interface IUInt8Embedder<I> : IEmbedder<byte, I>
    {
    }
}
