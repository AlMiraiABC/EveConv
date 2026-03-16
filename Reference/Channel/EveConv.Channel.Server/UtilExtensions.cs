using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Channel.Server
{
    internal static class UtilExtensions
    {
        public static string GetPath(this string query)
        {
            var idx = query.IndexOf('?');
            return idx < 0 ? query : query[..idx];
        }
    }
}
