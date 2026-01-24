using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.Embedder
{
    /// <summary>
    /// Generic interface for embedding models that using float32.
    /// </summary>
    /// <typeparam name="I">Type of input.</typeparam>
    public interface IFloat32Embedder<I> : IEmbedder<float, I>
    {
    }
}
