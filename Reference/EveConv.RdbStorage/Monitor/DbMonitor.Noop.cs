using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;

namespace EveConv.RdbStorage.Monitor;

public class NoopDbMonitor : IDbMonitor
{
    private readonly ILogger<NoopDbMonitor> _logger;

    public NoopDbMonitor(ILoggerFactory? loggerFactory = null)
    {
        _logger = loggerFactory?.CreateLogger<NoopDbMonitor>() ?? NullLogger<NoopDbMonitor>.Instance;
    }

    public void Log(string logLevel, string sql, DateTime startTime, DateTime? endTime = null, long? duration = null,
        string? trace = null)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("[{logLevel}] {startTime}~{endTime}[{duration}]: {sql} {trace}",
                logLevel, sql, startTime, endTime, duration, trace);
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
}
