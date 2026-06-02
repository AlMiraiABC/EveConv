using SqlSugar;

namespace EveConv.RdbStorage.Monitor;


public interface IDbMonitor
{
    void Log(string logLevel, string sql,
        DateTime startTime, DateTime? endTime = null, long? duration = null,
        string? trace = null);

    void Log(string sql, IAdo ado);
    void Log(SqlSugarException ex);
}

