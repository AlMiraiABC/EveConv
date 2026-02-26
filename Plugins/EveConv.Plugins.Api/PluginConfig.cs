using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Plugins.Api
{
    public class PluginConfig : Dictionary<string, object?>
    {
        public PluginConfig() : base(StringComparer.OrdinalIgnoreCase)
        {

        }

        public PluginConfig(IDictionary<string, object?> dictionary) : base(dictionary, StringComparer.OrdinalIgnoreCase)
        {
        }

        /// <summary>
        /// Converts this dictionary to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The specified type.</typeparam>
        /// <returns>Instance of <typeparamref name="T"/></returns>
        /// <exception cref="InvalidOperationException">Failed to deserialize.</exception>
        public T Deserialize<T>() where T : notnull
        {
            var json = System.Text.Json.JsonSerializer.SerializeToNode(this);
            return System.Text.Json.JsonSerializer.Deserialize<T>(json) ?? throw new InvalidOperationException($"Failed to deserialize plugin config to type {typeof(T).FullName}");
        }
    }
}
