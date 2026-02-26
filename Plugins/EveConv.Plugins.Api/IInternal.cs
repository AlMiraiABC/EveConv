using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Plugins.Api
{
    /// <summary>
    /// Private interface for internal use.
    /// </summary>
    /// <remarks>All members of this interface used for internal only, and may be changed without notice.</remarks>
    public interface IInternal : IPlugin
    {
        public T? GetService<T>() where T : notnull;
        public object? GetService(Type serviceType);
        public IEnumerable<T> GetServices<T>() where T : notnull;
        public T GetRequiredService<T>() where T : notnull;
        public object GetRequiredService(Type serviceType);
        public T? GetKeyedService<T>(object? key);
        public T GetRequiredKeyedService<T>(Type serviceType, object? key) where T : notnull;
    }
}
