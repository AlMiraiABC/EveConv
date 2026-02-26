using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Plugins.Api
{
    /// <summary>
    /// Base plugin interface. All plugins must implement this interface.
    /// </summary>
    public interface IPlugin
    {
        PluginContext Context { get; }

        public void OnConfigChanged()
        {

        }
    }
}
