using System;
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

namespace EveConv.Storage.Qdrant
{
    public class QdrantVectorStorage : IVectorStorage, IDisposable
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
            CheckCollectionName(collectionName);
            return await this._client.CollectionExistsAsync(collectionName, cancellationToken);
        }

        public async Task CreateCollectionAsync(string collectionName, int vectorDimension, CancellationToken cancellationToken = default)
        {
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
            foreach (var point in points)
            {
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

        public async Task<string> UpsertVectorAsync(string collectionName, VectorRecord vector, CancellationToken cancellationToken = default)
        {
            CheckCollectionName(collectionName);
            CheckVectorData(vector.Data);
            var id = !string.IsNullOrWhiteSpace(vector.Id)
                ? ParseVectorId(vector.Id)
                : Guid.NewGuid();
            var point = new PointStruct()
            {
                Id = id,
                Vectors = vector.Data,
                Payload = { }
            };
            FillPointPayload(point.Payload, vector.Metadata);
            await this._client.UpsertAsync(collectionName, [point], cancellationToken: cancellationToken);
            return id.ToString();
        }

        #region private

        [return: NotNull]
        private static void CheckCollectionName(string? collectionName)
        {
            if (string.IsNullOrWhiteSpace(collectionName))
            {
                throw new ArgumentException("Collection name cannot be null or empty", nameof(collectionName));
            }
        }

        [return: NotNull]
        private static void CheckVectorData(float[]? data)
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

        private static readonly JsonSerializerOptions PAYLOAD_SERIALIZER_OPTIONS = new JsonSerializerOptions()
        {
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
        };
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
            var ele = JsonSerializer.SerializeToElement(metadata, PAYLOAD_SERIALIZER_OPTIONS);
            foreach (var prop in ele.EnumerateObject())
            {
                payload[prop.Name] = ToValue(prop.Value);
            }
            static Value ToValue(JsonElement element)
            {
                var value = new Value();
                switch (element.ValueKind)
                {
                    case JsonValueKind.Object:
                        var structValue = new Struct();
                        foreach (var p in element.EnumerateObject())
                        {
                            structValue.Fields[p.Name] = ToValue(p.Value);
                        }
                        value.StructValue = structValue;
                        break;
                    case JsonValueKind.Array:
                        var list = new ListValue();
                        foreach (var item in element.EnumerateArray())
                        {
                            list.Values.Add(ToValue(item));
                        }
                        value.ListValue = list;
                        break;
                    case JsonValueKind.String:
                        value.StringValue = element.GetString();
                        break;
                    case JsonValueKind.Number:
                        var raw = element.GetRawText();
                        if (!raw.ContainsAny(['.', 'e', 'E']) && element.TryGetInt64(out var v))
                        {
                            value.IntegerValue = v;
                        }
                        else
                        {
                            value.DoubleValue = element.GetDouble();
                        }
                        break;
                    case JsonValueKind.True:
                        value.BoolValue = true;
                        break;
                    case JsonValueKind.False:
                        value.BoolValue = false;
                        break;
                    case JsonValueKind.Undefined:
                    case JsonValueKind.Null:
                    default:
                        value.NullValue = NullValue.NullValue;
                        break;
                }
                return value;
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
