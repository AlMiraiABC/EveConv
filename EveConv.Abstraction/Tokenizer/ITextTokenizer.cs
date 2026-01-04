using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.Tokenizer
{
    public interface ITextTokenizer<V> : ITokenizer<V, string>
    {
    }
}
