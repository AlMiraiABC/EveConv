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
        Task<ReadOnlyMemory<byte>> ExecuteAsync(ReadOnlyMemory<byte> input, IDictionary<string, object>? context = null, CancellationToken token = default);
    }

    /// <summary>
    /// Session for model inference.
    /// </summary>
    /// <typeparam name="I">Type of session instance.</typeparam>
    /// <typeparam name="P">Type of execute parameter.</typeparam>
    /// <typeparam name="O">Type of execute output.</typeparam>
    public abstract class InferenceSession<I, P, O> : IInferenceSession
    {
        protected bool _disposed;

        /// <summary>
        /// Actual session instance.
        /// </summary>
        public I Instance { get; set; }

        /// <summary>
        /// Create an inference session with specified instance.
        /// </summary>
        /// <param name="instance">The specified instance.</param>
        public InferenceSession(I instance)
        {
            ArgumentNullException.ThrowIfNull(instance);
            Instance = instance;
        }

        public async Task<ReadOnlyMemory<byte>> ExecuteAsync(ReadOnlyMemory<byte> input, IDictionary<string, object>? context = null, CancellationToken token = default)
        {
            var param = await ProcessInputAsync(input, context, token);
            var output = await ExecuteAsync(param, context, token);
            return await ProcessOutputAsync(output, context, token);
        }

        /// <summary>
        /// Pre process inputs.
        /// </summary>
        /// <param name="param">Input parameters.</param>
        /// <param name="context">Extra context.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task that contains a processed inputs.</returns>
        protected abstract Task<P> ProcessInputAsync(ReadOnlyMemory<byte> param, IDictionary<string, object>? context = null, CancellationToken token = default);

        /// <summary>
        /// Post process outputs.
        /// </summary>
        /// <param name="output">Output result.</param>
        /// <param name="context">Extra context.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task contains a processed outputs.</returns>
        protected abstract Task<ReadOnlyMemory<byte>> ProcessOutputAsync(O output, IDictionary<string, object>? context = null, CancellationToken token = default);

        /// <summary>
        /// Asynchronously execute an inference.
        /// </summary>
        /// <param name="param">Input parameters</param>
        /// <param name="context">Extra context.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>A task that contains a output result.</returns>
        public abstract Task<O> ExecuteAsync(P param, IDictionary<string, object>? context = null, CancellationToken token = default);

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
