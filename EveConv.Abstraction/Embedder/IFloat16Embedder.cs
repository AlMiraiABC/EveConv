using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.Embedder
{
    /// <summary>
    /// Generic interface for embedding models that using float16(<see cref="Half"/>).
    /// </summary>
    /// <typeparam name="I">Type of input.</typeparam>
    public interface IFloat16Embedder<I> : IEmbedder<Half, I>
    {
    }
}
