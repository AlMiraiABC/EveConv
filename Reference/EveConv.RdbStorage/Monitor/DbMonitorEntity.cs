using SqlSugar;

namespace EveConv.RdbStorage.Monitor;

[SugarTable("eve_conv_db_monitor")]
[SugarIndex("IDX_DB_MONITOR", nameof(Sql), OrderByType.Asc)]
public class DbMonitorEntity : BaseEntity
{
    /// <summary>
    /// SQL statement.
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public string Sql { get; set; } = string.Empty;

    /// <summary>
    /// SQL execution duration in milliseconds.
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public long ExecDuration { get; set; }

    /// <summary>
    /// SQL execution start time.
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTime ExecStartTime { get; set; }

    /// <summary>
    /// SQL execution end time.
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTime ExecEndTime { get; set; }

    /// <summary>
    /// Trace information.
    /// </summary>
    public string? ExecTrace { get; set; }

    /// <summary>
    /// Log level.
    /// </summary>
    public string LogLevel { get; set; } = "Info";
}
