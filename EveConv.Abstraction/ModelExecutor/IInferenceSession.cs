using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.ModelExecutor
{
    /// <summary>
    /// Interface for inference session.
    /// </summary>
    public interface IInferenceSession : IDisposable
    {
        /// <summary>
        /// Execute an inference.
        /// </summary>
        /// <param name="input">Input parameters.</param>
        /// <param name="context">Extra context.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>A task that contains a output results.</returns>
        Task<object?> ExecuteAsync(object input, IDictionary<string, object>? context = null, CancellationToken token = default);
    }

    /// <summary>
    /// Session for model inference.
    /// </summary>
    /// <typeparam name="Ins">Type of session instance.</typeparam>
    /// <typeparam name="In">Type of execute parameter.</typeparam>
    /// <typeparam name="Out">Type of execute result.</typeparam>
    public abstract class InferenceSession<Ins, In, Out> : IInferenceSession
    {
        protected bool _disposed;

        /// <summary>
        /// Actual session instance.
        /// </summary>
        public Ins Instance { get; set; }

        /// <summary>
        /// Create an inference session with specified instance.
        /// </summary>
        /// <param name="instance">The specified instance.</param>
        public InferenceSession(Ins instance)
        {
            ArgumentNullException.ThrowIfNull(instance);
            Instance = instance;
        }

        public virtual async Task<object?> ExecuteAsync(object input, IDictionary<string, object>? context = null, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(input);
            if (input is not In inParam)
            {
                throw new NotSupportedException($"Input type of {input.GetType()} is not supported. Only {typeof(In)} is supported.");
            }
            if (context is not null)
            {
                context = new Dictionary<string, object>(context, StringComparer.OrdinalIgnoreCase);
            }
            return await ExecuteAsync(inParam, context, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Execute an inference with specified type.
        /// </summary>
        /// <param name="input">Input parameters.</param>
        /// <param name="context">Extra context.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>A task that contains a output results.</returns>
        public abstract Task<Out> ExecuteAsync(In input, IDictionary<string, object>? context = null, CancellationToken token = default);

        protected virtual T? GetContextValue<T>(IDictionary<string, object>? context, string key, T? defaultValue = default)
        {
            if (context is not null && context.TryGetValue(key, out var v) && v is T value)
            {
                return value;
            }
            return defaultValue;
        }

        public virtual void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            GC.SuppressFinalize(this);
            return;
        }
    }
}
