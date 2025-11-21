using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using EveConv.Abstraction.VectorStorage;
using Microsoft.Extensions.Logging;
using Qdrant.Client.Grpc;

namespace EveConv.VectorStorage.Qdrant
{
    public partial class QdrantVectorStorage : IBatchVectorStorage
    {
        public async Task<IEnumerable<string>> UpsertVectorBatchAsync(string collectionName, IEnumerable<VectorRecord> records, CancellationToken cancellationToken = default)
        {
            CheckCollectionName(collectionName);
            ArgumentNullException.ThrowIfNull(records);
            var recordList = records.ToList();
            if (recordList.Count == 0)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("No vectors to upsert into collection '{cname}'", collectionName);
                }
                return [];
            }
            var points = recordList.Select(record =>
                {
                    CheckInsertVectorRecord(record);
                    var id = !string.IsNullOrWhiteSpace(record.Id)
                        ? ParseVectorId(record.Id)
                        : Guid.NewGuid();
                    var point = new PointStruct()
                    {
                        Id = id,
                        Vectors = record.Data,
                        Payload = { }
                    };
                    FillPointPayload(point.Payload, record.Metadata);
                    return point;
                })
                .ToList();
            await this._client.UpsertAsync(collectionName, points, cancellationToken: cancellationToken);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Upserted {count} vectors into collection '{cname}'", points.Count, collectionName);
            }
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                var states = points.Zip(recordList)
                    .GroupBy(i => string.IsNullOrWhiteSpace(i.Second.Id))
                    .Select(i => i.Select(j => j.First.Id.Uuid).ToList())
                    .ToList();
                var inserted = states[0];
                var updated = states[1];
                _logger.LogTrace("Inserted vector Ids: [{ids}]", string.Join(", ", inserted));
                _logger.LogTrace("Updated vector Ids: [{ids}]", string.Join(", ", updated));
            }
            return points.Select(p => p.Id.Uuid).ToList();
        }
    }
}
