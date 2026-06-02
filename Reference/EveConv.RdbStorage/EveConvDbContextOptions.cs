using Microsoft.Extensions.Options;

namespace EveConv.RdbStorage;

public class EveConvDbContextOptions : IOptions<EveConvDbContextOptions>
{
    public SqlSugar.DbType DbType { get; set; } = SqlSugar.DbType.Sqlite;
    
    public TimeSpan SlowExecThreshold { get; set; } = TimeSpan.FromSeconds(10);
    public int LogMaxQueueSize { get; set; } = 10000;
    
    public EveConvDbContextOptions Value => this;
}
