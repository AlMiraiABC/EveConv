using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Plugins.Api
{
    public delegate void ConfigChangedHandler(PluginConfig newConfig);

    public record PluginContext
    {
        public PluginContext(string installPath, PluginInfo info)
        {
            Info = info;
            InstallPath = installPath;
        }

        public PluginContext(string installPath, PluginInfo info, PluginConfig config) : this(installPath, info)
        {
            this.Config = config;
        }

        public PluginConfig Config
        {
            get => field ?? [];
            set
            {
                OnConfigChanged?.Invoke(value);
                field = value;
            }
        }

        public PluginInfo Info { get; init; }

        public string InstallPath { get; init; }

        public event ConfigChangedHandler OnConfigChanged = delegate { };
    }
}
