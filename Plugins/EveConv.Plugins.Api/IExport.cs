using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Plugins.Api
{
    /// <summary>
    /// Exported functions for this plugins provided to others.
    /// </summary>
    public interface IExport : IPlugin
    {
        /// <summary>
        /// Exported function dictionary with function name and description.
        /// </summary>
        List<ExportedFunction> Exports { get; }

    }

    public struct ExportedFunction(string name)
    {
        /// <summary>
        /// The unique name of the exported function. The prefix of function name doesn't include <see cref="IPlugin.Id"/>.
        /// </summary>
        public readonly string Name => name;
        /// <summary>
        /// Description for this exported function.
        /// </summary>
        public string Description { get; set; } = string.Empty;
        /// <summary>
        /// Optional arguments.
        /// </summary>
        public List<ExportedFunctionArg> Args { get; set; } = [];
        /// <summary>
        /// Return type of this function. If not set, it will be treated as void return type.
        /// </summary>
        public Type ReturnType { get; set; } = typeof(void);
    }

    public class ExportedFunctionArg(string name)
    {
        /// <summary>
        /// The unique name of the argument.
        /// </summary>
        public string Name => name;
        /// <summary>
        /// Description for this argument.
        /// </summary>
        public string Description { get; set; } = string.Empty;
        /// <summary>
        /// Whether this argument is required. If not required, the default value will be used when not set.
        /// </summary>
        public bool IsRequired { get; set; } = false;
    }

    public class ExportedFunctionArg<T>(string name) : ExportedFunctionArg(name)
    {
        /// <summary>
        /// Default value when not set.
        /// </summary>
        public T? DefaultValue { get; set; } = default;
    }
}
