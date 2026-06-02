using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;

namespace EveConv.RdbStorage.Monitor;

public class DbMonitor : IAsyncDisposable, IDbMonitor
{
    public const int DefaultMaxQueueLength = 10000;

    private readonly ISqlSugarClient _db;
    private readonly ILogger _logger;
    private readonly Channel<DbMonitorEntity> _channel;
    private readonly CancellationTokenSource _cts;
    private readonly Task _consumerTask;
    private long _droppedCount;

    /// <summary>
    /// Number of log entries dropped due to queue overflow.
    /// </summary>
    public long DroppedCount => Interlocked.Read(ref _droppedCount);

    public DbMonitor(ISqlSugarClient db,
        int maxQueueLength = DefaultMaxQueueLength,
        ILoggerFactory? loggerFactory = null)
    {
        _db = db;
        _logger = loggerFactory?.CreateLogger<DbMonitor>() ?? NullLogger<DbMonitor>.Instance;
        _cts = new CancellationTokenSource();

        var options = new BoundedChannelOptions(maxQueueLength)
        {
            FullMode = BoundedChannelFullMode.DropWrite
        };
        _channel = Channel.CreateBounded<DbMonitorEntity>(options);
        _consumerTask = ConsumeAsync(_cts.Token);
    }

    /// <summary>
    /// Background consumer: drains the FIFO queue and writes to DB one by one.
    /// </summary>
    private async Task ConsumeAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var entity in _channel.Reader.ReadAllAsync(ct))
            {
                try
                {
                    await _db.Insertable(entity).ExecuteCommandAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "DbMonitor: failed to write log entry to DB");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
    }

    public void Log(string logLevel, string sql,
        DateTime startTime, DateTime? endTime = null, long? duration = null,
        string? trace = null)
    {
        endTime ??= DateTime.Now;
        duration ??= (long)(endTime.Value - startTime).TotalMilliseconds;
        var row = new DbMonitorEntity()
        {
            Sql = sql,
            ExecDuration = duration.Value,
            ExecStartTime = startTime,
            ExecEndTime = endTime.Value,
            ExecTrace = trace,
            LogLevel = logLevel,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
        };

        if (_channel.Writer.TryWrite(row))
        {
            return;
        }
        var dropped = Interlocked.Increment(ref _droppedCount);
        if (dropped != 1 && dropped % 1000 != 0)
        {
            return;
        }
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning("DbMonitor: log queue overflow, dropped {Count} entries total", dropped);
        }
    }

    public void Log(string sql, IAdo ado)
    {
        try
        {
            var duration = ado.SqlExecutionTime;
            var endTime = DateTime.Now;
            var startTime = endTime - duration;
            var trace =
                $"{ado.SqlExecuteType} [{ado.SqlStackTrace.FirstMethodName}]" +
                $" {ado.SqlStackTrace.FirstFileName}#L{ado.SqlStackTrace.FirstLine}";
            Log("Info", sql, startTime, endTime, (long)duration.TotalMilliseconds, trace);
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(ex, "DbMonitor: failed to write log entry to DB with sql {sql}", sql);
            }
        }
    }

    public void Log(SqlSugarException ex)
    {
        try
        {
            const long duration = 0L;
            var startTime = DateTime.Now;
            var endTime = startTime;
            var sql = ex.Sql;
            var trace = $"[{ex.Source}] {ex.StackTrace}";
            Log("Error", sql, startTime, endTime, duration, trace);
        }
        catch (Exception e)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(e, "DbMonitor: failed to write log entry to DB with ex {ex}", ex);
            }
        }
    }

    /// <summary>
    /// Wait until the queue is drained and the consumer has flushed all pending writes.
    /// Useful for testing scenarios where assertions depend on persisted log entries.
    /// </summary>
    public async Task WaitForDrainAsync(TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(5);
        using var drainCts = new CancellationTokenSource(timeout.Value);
        try
        {
            while (_channel.Reader.Count > 0)
            {
                await Task.Delay(10, drainCts.Token);
            }
            // Extra delay for consumer to finish its current write
            await Task.Delay(50, drainCts.Token);
        }
        catch (OperationCanceledException)
        {
            // timeout, give up
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _channel.Writer.TryComplete();
        try
        {
            await _consumerTask;
        }
        catch
        {
            // do nothing
        }
        _db.Dispose();
        _cts.Dispose();
        _consumerTask.Dispose();
        GC.SuppressFinalize(this);
    }
}
