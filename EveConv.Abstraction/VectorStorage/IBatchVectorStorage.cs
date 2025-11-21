using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.VectorStorage
{
    public interface IBatchVectorStorage : IVectorStorage
    {

        /// <summary>
        /// Asynchronously inserts a set of new vectors or updates an existing vector in the specified collection.
        /// </summary>
        /// <param name="collectionName">The name of the collection in which to upsert the vector. Cannot be null or empty.</param>
        /// <param name="records">A collection of vector data to upsert.</param>
        /// <param name="cancellationToken">Task cancellation token</param>
        /// <returns>
        ///     A task that represents the asynchronous upsert operation.
        ///     The task result contains the unique identifiers of the upserted vectors.
        /// </returns>
        /// <remarks>
        ///     Insert if the vector with the specified ID does not exist;
        ///     otherwise, update the existing vector with the new data and metadata.
        /// </remarks>
        Task<IEnumerable<string>> UpsertVectorBatchAsync(string collectionName, IEnumerable<VectorRecord> records, CancellationToken cancellationToken = default);

    }
}
