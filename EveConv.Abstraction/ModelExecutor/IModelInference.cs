using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.ModelExecutor
{
    public interface IModelInference
    {
        /// <summary>
        /// Asynchronously create an inference session from specified model file.
        /// </summary>
        /// <param name="modelPath">Path of model file.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>A task that contains an inference session.</returns>
        Task<IInferenceSession> CreateSessionAsync(string modelPath, CancellationToken token = default);
        /// <summary>
        /// Asynchronously create an inference session from specified model stream.
        /// </summary>
        /// <param name="model">Content stream of model.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>A task that contains an inference session.</returns>
        Task<IInferenceSession> CreateSessionAsync(Stream model, CancellationToken token = default);
    }

    /// <summary>
    /// A generic interface for model inference.
    /// </summary>
    /// <typeparam name="S">Type of inference session.</typeparam>
    /// <typeparam name="O">Type of configuration options.</typeparam>
    public interface IModelInference<O, S> : IModelInference
        where O : class
        where S : IInferenceSession
    {
        /// <summary>
        /// Asynchronously create an inference session from specified model file with options.
        /// </summary>
        /// <param name="modelPath">Path of model file.</param>
        /// <param name="options">Create options.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>A task that contains an inference session.</returns>
        Task<S> CreateSessionAsync(string modelPath, O? options, CancellationToken token = default);
        /// <summary>
        /// Asynchronously create an inference session from specified model stream with options.
        /// </summary>
        /// <param name="model">Content stream of model.</param>
        /// <param name="options">Create options.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>A task that contains an inference session.</returns>
        Task<S> CreateSessionAsync(Stream model, O? options, CancellationToken token = default);
    }

}
