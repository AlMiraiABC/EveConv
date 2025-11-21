using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.VectorStorage
{
    /// <summary>
    /// Context for search operations.
    /// </summary>
    public sealed class SearchContext : IContext
    {
        public IDictionary<string, object?> Arguments { get; set; } = new Dictionary<string, object?>();

        public SearchContext()
        {
        }

        public SearchContext(IDictionary<string, object?>? arguments)
        {
            Arguments = arguments ?? new Dictionary<string, object?>();
        }
    }
}
