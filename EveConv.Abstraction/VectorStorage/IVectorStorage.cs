using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace EveConv.Abstraction.VectorStorage
{
    public interface IVectorStorage
    {
        /// <summary>
        /// Asynchronously determines whether a collection with the specified name exists.
        /// </summary>
        /// <param name="collectionName">The name of the collection to check for existence. Cannot be null or empty.</param>
        /// <param name="cancellationToken">Task cancellation token</param>
        /// <returns>
        ///     A task that represents the asynchronous operation
        ///     that contains <see langword="true"/> if the collection exists; otherwise, <see langword="false"/>.
        /// </returns>
        Task<bool> CheckCollectionExistsAsync(string collectionName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously creates a new vector collection with the specified name and vector dimension.
        /// </summary>
        /// <param name="collectionName">The name of the collection to create. Cannot be null or empty.</param>
        /// <param name="vectorDimension">The number of dimensions for vectors in the collection. Must be a positive integer.</param>
        /// <param name="cancellationToken">Task cancellation token</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task CreateCollectionAsync(string collectionName, int vectorDimension, CancellationToken cancellationToken = default);

        /// <summary>
        /// Ensures that a collection with the specified name and vector dimension exists, creating it if necessary.
        /// </summary>
        /// <param name="collectionName">The name of the collection to check for existence or create. Cannot be null or empty.</param>
        /// <param name="vectorDimension">The number of dimensions for vectors stored in the collection. Must be a positive integer.</param>
        /// <param name="cancellationToken">Task cancellation token</param>
        /// <returns>
        ///     A task that represents the asynchronous operation. 
        ///     The task completes when the collection is confirmed to exist.
        /// </returns>
        Task EnsureCollectionExistsAsync(string collectionName, int vectorDimension, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously inserts a new vector or updates an existing vector in the specified collection.
        /// </summary>
        /// <param name="collectionName">The name of the collection in which to upsert the vector. Cannot be null or empty.</param>
        /// <param name="vector">The vector data to upsert. Cannot be null.</param>
        /// <param name="cancellationToken">Task cancellation token</param>
        /// <returns>
        ///     A task that represents the asynchronous upsert operation.
        ///     The task result contains the unique identifier of the upserted vector.
        /// </returns>
        /// <remarks>
        ///     Insert if the vector with <paramref name="vectorId"/> is empty or does not exist;
        ///     otherwise, update the existing vector with the new data and metadata.
        /// </remarks>
        Task<string> UpsertVectorAsync(string collectionName, VectorRecord vector, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously deletes a vector with the specified identifier from the given collection.
        /// </summary>
        /// <param name="collectionName">The name of the collection from which to delete the vector. Cannot be null or empty.</param>
        /// <param name="vectorId">The unique identifier of the vector to delete. Cannot be null or empty.</param>
        /// <param name="cancellationToken">Task cancellation token</param>
        /// <returns>A task that represents the asynchronous delete operation.</returns>
        Task DeleteVectorAsync(string collectionName, string vectorId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously searches for the top N most similar vectors in the specified collection
        /// based on the provided query vector.
        /// </summary>
        /// <param name="collectionName">The name of the collection to search within. Cannot be null or empty.</param>
        /// <param name="queryVector">
        ///     The vector to use as the query for similarity search.
        ///     Cannot be null and must match the dimensionality of vectors in the collection.
        /// </param>
        /// <param name="filter">Optional filter to apply to the search results.</param>
        /// <param name="context">Optional context for search.</param>
        /// <param name="minRelevance">The minmum similarity score.</param>
        /// <param name="limit">The maximum number of most similar vector items to return. Unlimit to set to 0.</param>
        /// <param name="offset">The number of most similar vector items to skip before starting to collect the result set.</param>
        /// <param name="cancellationToken">Task cancellation token</param>
        /// <returns>
        ///     An async enumerable of the top N most similar vector items and its similarity, ordered by similarity.
        ///     The enumerable will be empty if no similar vectors are found.
        /// </returns>
        IAsyncEnumerable<(VectorRecord, float Score)> SearchVectorsAsync(
            string collectionName,
            float[] queryVector,
            Searchable? filter = null,
            SearchContext? context = null,
            float minRelevance = 0,
            int limit = 1,
            int offset = 0,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a vector and its associated metadata.
    /// </summary>
    public record VectorRecord
    {
        /// <summary>
        /// The unique identifier of the vector.
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// Payload metadata associated with the vector.
        /// </summary>
        public Dictionary<string, object?>? Metadata;

        /// <summary>
        /// Raw vector data as an array.
        /// </summary>
        public float[] Data { get; init; } = [];
    }
}
