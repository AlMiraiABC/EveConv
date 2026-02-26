using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace EveConv.Plugins.Api.Abstraction
{
    public class InternalExport : IInternal
    {
        private readonly IServiceProvider _services;

        public InternalExport(PluginContext context, IServiceProvider services)
        {
            Context = context;
            this._services = services;
        }

        public PluginContext Context { get; }

        public T GetRequiredKeyedService<T>(Type serviceType, object? key) where T : notnull
        {
            return _services.GetRequiredKeyedService<T>(key);
        }

        public T? GetKeyedService<T>(object? key)
        {
            return _services.GetKeyedService<T>(key);
        }

        public T GetRequiredService<T>() where T : notnull
        {
            return _services.GetRequiredService<T>();
        }

        public object GetRequiredService(Type serviceType)
        {
            return _services.GetRequiredService(serviceType);
        }

        public T? GetService<T>() where T : notnull
        {
            return _services.GetService<T>();
        }

        public object? GetService(Type serviceType)
        {
            return _services.GetService(serviceType);
        }

        public IEnumerable<T> GetServices<T>() where T : notnull
        {
            return _services.GetServices<T>();
        }
    }
}
