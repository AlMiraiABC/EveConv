using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Xml.Linq;
using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.VectorStorage;
using Google.Protobuf.Collections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace EveConv.VectorStorage.Qdrant
{
    public partial class QdrantVectorStorage : IVectorStorage, IDisposable
    {
        private bool _disposed;

        private readonly QdrantConfiguration _configuration;
        private readonly ILogger<QdrantVectorStorage> _logger;
        private readonly QdrantClient _client;

        public QdrantVectorStorage(IOptions<QdrantConfiguration> configuration, ILoggerFactory? loggerFactory)
        {
            this._configuration = configuration?.Value ?? throw new ArgumentNullException(nameof(configuration));
            this._configuration.Valid();
            loggerFactory ??= DefaultLogger.Factory;
            this._logger = loggerFactory.CreateLogger<QdrantVectorStorage>();
            this._client = new QdrantClient(_configuration.Endpoint!, _configuration.ApiKey, _configuration.Timeout, loggerFactory);
        }

        public async Task<bool> CheckCollectionExistsAsync(string collectionName, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            CheckCollectionName(collectionName);
            return await this._client.CollectionExistsAsync(collectionName, cancellationToken);
        }

        public async Task CreateCollectionAsync(string collectionName, int vectorDimension, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            CheckCollectionName(collectionName);
            if (vectorDimension <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(vectorDimension), "The vector dimension must be a positive number.");
            }
            _logger.LogWarning("Create collection is not recommended, please optimize it manually.");
            await this._client.CreateCollectionAsync(collectionName, new VectorParams()
            {
                Size = (uint)vectorDimension,
            }, cancellationToken: cancellationToken);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Collection {cname} created with size {dimension}", collectionName, vectorDimension);
            }
        }

        public async Task DeleteVectorAsync(string collectionName, string vectorId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            CheckCollectionName(collectionName);
            await this._client.DeleteAsync(collectionName, ParseVectorId(vectorId), cancellationToken: cancellationToken);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Vector {vid} of {cname} deleted", vectorId, collectionName);
            }
        }

        private readonly SemaphoreSlim _ensureCollectionExistsLock = new(1, 1);
        public async Task EnsureCollectionExistsAsync(string collectionName, int vectorDimension, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            CheckCollectionName(collectionName);
            if (vectorDimension <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(vectorDimension), "The vector dimension must be a positive number.");
            }
            if (await CheckCollectionExistsAsync(collectionName, cancellationToken))
            {
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("Collection {cname} has exists, skip to create", collectionName);
                }
                return;
            }
            await _ensureCollectionExistsLock.WaitAsync(cancellationToken);
            try
            {
                // double check
                if (await CheckCollectionExistsAsync(collectionName, cancellationToken))
                {
                    if (_logger.IsEnabled(LogLevel.Trace))
                    {
                        _logger.LogTrace("Collection {cname} has exists, skip to create", collectionName);
                    }
                    return;
                }
                await CreateCollectionAsync(collectionName, vectorDimension, cancellationToken);
            }
            finally
            {
                _ensureCollectionExistsLock.Release();
            }
        }

        public async IAsyncEnumerable<(VectorRecord, float Score)> SearchVectorsAsync(
            string collectionName,
            float[] queryVector,
            Searchable? filter = null,
            SearchContext? context = null,
            float minRelevance = 0,
            int limit = 1,
            int offset = 0,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            CheckCollectionName(collectionName);
            if (limit <= 0)
            {
                limit = int.MaxValue;
            }
            if (offset <= 0)
            {
                offset = 0;
            }
            var points = await this._client.SearchAsync(
                collectionName,
                queryVector,
                FilterBuilder.Build(filter),
                payloadSelector: true,
                vectorsSelector: true,
                scoreThreshold: minRelevance,
                limit: (ulong)limit,
                offset: (ulong)offset,
                cancellationToken: cancellationToken);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Searched {count} vectors from collection {cname} with limit {limit} and offset {offset}",
                    points.Count, collectionName, limit, offset);
            }
            foreach (var point in points)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var id = point.Id.Uuid;
                var metadata = ParsePointPayload(point.Payload);
                var vector = point.Vectors?.Vector?.Data?.ToArray() ?? [];
                var record = new VectorRecord()
                {
                    Id = id,
                    Data = vector,
                    Metadata = metadata,
                };
                yield return (record, point.Score);
            }
        }

        public async Task<string> UpsertVectorAsync(string collectionName, VectorRecord record, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            CheckCollectionName(collectionName);
            CheckInsertVectorRecord(record);
            var point = new PointStruct()
            {
                Id = !string.IsNullOrWhiteSpace(record.Id)
                    ? ParseVectorId(record.Id)
                    : Guid.NewGuid(),
                Vectors = record.Data,
                Payload = { }
            };
            FillPointPayload(point.Payload, record.Metadata);
            await this._client.UpsertAsync(collectionName, [point], cancellationToken: cancellationToken);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                if (string.IsNullOrWhiteSpace(record.Id))
                {
                    _logger.LogDebug("Vector {vid} inserted to collection {cname}", point.Id.Uuid, collectionName);
                }
                else
                {
                    _logger.LogDebug("Vector {vid} updated to collection {cname}", point.Id.Uuid, collectionName);
                }
            }
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Vector {vid} upserted to collection {cname} with vector {vector} and payload {payload}",
                    point.Id.Uuid, collectionName, JsonSerializer.Serialize(record.Data), JsonSerializer.Serialize(record.Metadata));
            }
            return point.Id.Uuid.ToString();
        }

        #region private

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckCollectionName([NotNull] string? collectionName)
        {
            if (string.IsNullOrWhiteSpace(collectionName))
            {
                throw new ArgumentException("Collection name cannot be null or empty", nameof(collectionName));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckInsertVectorRecord([NotNull] VectorRecord? record)
        {
            ArgumentNullException.ThrowIfNull(record);
            if (!string.IsNullOrWhiteSpace(record.Id))
            {
                CheckVectorData(record.Data);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckVectorData([NotNull] float[]? data)
        {
            if (data is null or [])
            {
                throw new ArgumentException("Vector data cannot be null or empty", nameof(data));
            }
        }

        /// <summary>
        /// Parse vector id to guid.
        /// </summary>
        /// <param name="vectorId">Vector id.</param>
        /// <returns>Parsed guid.</returns>
        /// <exception cref="ArgumentException">This vector id is malformed.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Guid ParseVectorId(string vectorId)
        {
            if (Guid.TryParse(vectorId, out var vid))
            {
                return vid;
            }
            throw new ArgumentException("Vector id should be a valid GUID", nameof(vectorId));
        }

        /// <summary>
        /// Parse qdrant point.payload to dictionary.
        /// </summary>
        /// <param name="payload"><see cref="PointStruct.Payload"/></param>
        /// <returns>Parsed dictionary. Or an empty dictionary if <see langword="null"/>.</returns>
        private static Dictionary<string, object?> ParsePointPayload(MapField<string, Value>? payload)
        {
            return ParseMapField(payload);
            static Dictionary<string, object?> ParseMapField(MapField<string, Value>? fields)
            {
                if (fields is null)
                {
                    return [];
                }
                return fields.ToDictionary(i => i.Key, i => ParseValue(i.Value));
            }
            static object? ParseValue(Value value)
            {
                return value.KindCase switch
                {
                    Value.KindOneofCase.NullValue => null,
                    Value.KindOneofCase.BoolValue => value.BoolValue,
                    Value.KindOneofCase.StringValue => value.StringValue,
                    Value.KindOneofCase.IntegerValue => value.IntegerValue,
                    Value.KindOneofCase.DoubleValue => value.DoubleValue,
                    Value.KindOneofCase.ListValue => value.ListValue.Values.Select(i => ParseValue(i)).ToList(),
                    Value.KindOneofCase.StructValue => value.StructValue.Fields.ToDictionary(i => i.Key, i => ParseValue(i.Value)),
                    _ => null,

                };
            }
        }

        /// <summary>
        /// Populates the specified payload with key-value pairs from the provided metadata dictionary, converting each
        /// value to a protocol buffer Value representation.
        /// </summary>
        /// <remarks>
        ///     Each entry in the metadata dictionary is serialized to a protocol buffer Value,
        ///     preserving the structure and types of nested objects and arrays. Only top-level entries from the metadata
        ///     are added to the payload.
        /// </remarks>
        /// <param name="payload">The payload to populate with metadata entries. Existing entries with matching keys will be overwritten.</param>
        /// <param name="metadata">A dictionary containing metadata to add to the payload. If null or empty, the payload is not modified.</param>
        private static void FillPointPayload(MapField<string, Value> payload, Dictionary<string, object?>? metadata)
        {
            if (metadata is null || metadata.Count == 0)
            {
                return;
            }
            foreach (var kv in metadata)
            {
                payload[kv.Key] = ToValue(kv.Value);
            }
            static Value ToValue(object? obj)
            {
                var v = new Value();
                if (obj is null)
                {
                    v.NullValue = NullValue.NullValue;
                    return v;
                }
                switch (obj)
                {
                    case string s:
                        v.StringValue = s;
                        return v;
                    case bool b:
                        v.BoolValue = b;
                        return v;
                    case byte or sbyte or short or ushort or int or uint or long:
                        v.IntegerValue = Convert.ToInt64(obj);
                        return v;
                    case float or double or decimal:
                        v.DoubleValue = Convert.ToDouble(obj);
                        return v;
                    case IEnumerable enumerable when obj is not string:
                        {
                            var listValue = new ListValue();
                            foreach (var item in enumerable)
                            {
                                listValue.Values.Add(ToValue(item));
                            }
                            v.ListValue = listValue;
                            return v;
                        }
                    case IDictionary<string, object?> dict:
                        {
                            var structValue = new Struct();
                            foreach (var entry in dict)
                            {
                                structValue.Fields[entry.Key] = ToValue(entry.Value);
                            }
                            v.StructValue = structValue;
                            return v;
                        }
                    default:
                        // fallback to json serialization
                        try
                        {
                            var json = JsonSerializer.Serialize(obj);
                            v.StringValue = json;
                        }
                        catch
                        {
                            v.NullValue = NullValue.NullValue;
                        }
                        return v;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _ensureCollectionExistsLock.Dispose();
            _client?.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion


    }
}
