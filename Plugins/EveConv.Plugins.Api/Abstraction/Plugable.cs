using System;
using System.Collections.Generic;
using System.Text;
using EveConv.Abstraction.Diagnostic;
using Microsoft.Extensions.Logging;

namespace EveConv.Plugins.Api.Abstraction
{
    public partial class Plugable : ILifeCycle
    {
        protected readonly ILoggerFactory _loggerFactory;

        private readonly ILogger<Plugable> _logger;
        private readonly string _pluginIdF;

        protected Plugable(PluginContext context, ILoggerFactory? loggerFactory = null)
        {
            Context = context;
            _loggerFactory = loggerFactory ?? DefaultLogger.Factory;
            _logger = _loggerFactory.CreateLogger<Plugable>();
            _pluginIdF = $"plugin {Context.Info.Id} v{Context.Info.Version}";
        }

        public PluginContext Context { get; }

        public void OnInstall()
        {
            LogEventInfo(_logger, "Installing", _pluginIdF);
        }

        public void AfterInstall()
        {
            LogEventInfo(_logger, "Installed", _pluginIdF);
        }

        public void OnUninstall()
        {
            LogEventInfo(_logger, "Uninstalling", _pluginIdF);
        }

        //public void AfterUninstall()
        //{
        //    LogEventInfo(_logger, "Uninstalled", _pluginIdF);
        //}

        public void OnUpdate()
        {
            LogEventInfo(_logger, "Updating", _pluginIdF);
        }

        public void AfterUpdate()
        {
            LogEventInfo(_logger, "Updated", _pluginIdF);
        }

        public void OnActivate()
        {
            LogEventInfo(_logger, "Activating", _pluginIdF);
        }

        public void AfterActivate()
        {
            LogEventInfo(_logger, "Activated", _pluginIdF);
        }

        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Debug,
           Message = "{@event} plugin {message}")]
        private static partial void LogEventInfo(ILogger logger, string @event, string message);
    }
}
