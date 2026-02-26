using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Plugins.Api
{
    public record PluginInfo
    {
        /// <summary>
        /// Unique identifier for the whole plugin repo.
        /// </summary>
        public string Id { get; init; } = string.Empty;
        /// <summary>
        /// Human readable name for this plugin.
        /// </summary>
        public string Name { get; init; } = string.Empty;
        /// <summary>
        /// Current version of this plugin instance.
        /// </summary>
        public Version Version { get; init; } = new Version(0, 0, 0);
        /// <summary>
        /// Short description for this plugin.
        /// </summary>
        public string Description { get; init; } = string.Empty;
        /// <summary>
        /// Author name or organization name who maintains this plugin.
        /// </summary>
        public string Author { get; init; } = string.Empty;
        /// <summary>
        /// Homepage URL for this plugin.
        /// </summary>
        public string Homepage { get; init; } = string.Empty;
    }
}
