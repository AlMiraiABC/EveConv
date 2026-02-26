using System;
using System.Collections.Generic;
using System.Text;
using EveConv.Abstraction.Diagnostic;
using EveConv.Plugins.Api;
using EveConv.Plugins.Api.Abstraction;
using Microsoft.Extensions.Logging;

namespace EveConv.Plugins.Qwen3Embedding
{
    public partial class Plugin : Plugable, IDisposable
    {
        private readonly ILogger<Plugin> _logger;
        private readonly IInternal _internalExport;
        private bool _disposed = false;

        private Config _config;

        public Plugin(PluginContext context, IInternal internalExport, ILoggerFactory? loggerFactory = null) : base(context, loggerFactory)
        {
            this._logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<Plugin>();
            this._internalExport = internalExport;
            this._config = context.Config.Deserialize<Config>();
            this.Context.OnConfigChanged += OnConfigChanged;
        }

        private void OnConfigChanged(PluginConfig newConfig)
        {
            _getGeneratorLock.Wait();
            try
            {
                var newC = newConfig.Deserialize<Config>();
                if (newC.Model != _config.Model)
                {
                    _generator?.Dispose();
                    _generator = null;
                }
                if (newC.Quantized != _config.Quantized
                    || newC.OrtOptimizedSaveExtension != _config.OrtOptimizedSaveExtension
                    || newC.OrtOptimizedSaveFolder != _config.OrtOptimizedSaveFolder
                    || newC.OrtGraphOptimizationLevel != _config.OrtGraphOptimizationLevel)
                {
                    (_modelInference as IDisposable)?.Dispose();
                    _modelInference = null;
                    _generator?.Dispose(); // _generator based on _modelInference.
                    _generator = null;
                }
                _config = newC;
            }
            finally
            {
                _getGeneratorLock.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _generator?.Dispose();
            _generator = null;
            (_modelInference as IDisposable)?.Dispose();
            _modelInference = null;
            _getGeneratorLock.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
