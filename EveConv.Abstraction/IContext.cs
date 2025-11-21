using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction
{
    public interface IContext
    {
        IDictionary<string, object?> Arguments { get; set; }
    }
}
