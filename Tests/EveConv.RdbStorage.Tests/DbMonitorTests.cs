using EveConv.RdbStorage.Monitor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace EveConv.RdbStorage.Tests;

public class DbMonitorTests : IAsyncDisposable
{
    private readonly IServiceProvider _sp;
    private readonly ISqlSugarClient _db;

    public DbMonitorTests()
    {
        var configuration = new ConfigurationManager();
        var services = new ServiceCollection();
        services
            .AddSingleton(Options.Create(new EveConvDbContextOptions()
            {
                SlowExecThreshold = TimeSpan.FromMilliseconds(1),
            }))
            .AddDbMonitor(configuration)
            .AddDbContext(configuration);
        _sp = services.BuildServiceProvider();
        _db = _sp.GetRequiredService<ISqlSugarClient>();
        _db.DbMaintenance.CreateDatabase();
        _db.CodeFirst.InitTables(typeof(DbMonitorEntity), typeof(TestEntity));
    }

    [Fact]
    public async Task Log_Insert_Success()
    {
        var row = new TestEntity()
        {
            Key = Guid.NewGuid().ToString(),
            Value = Guid.NewGuid().ToString(),
        };
        await _db.Insertable(row).ExecuteCommandAsync(TestContext.Current.CancellationToken);
        var log = await _db.Queryable<DbMonitorEntity>()
            .OrderByDescending(i => i.CreatedAt)
            .FirstAsync(TestContext.Current.CancellationToken);
        Console.WriteLine(log.ExecTrace);
        Assert.NotNull(log);
        Assert.Equal("Info", log.LogLevel, StringComparer.OrdinalIgnoreCase);
        Assert.True(log.ExecDuration > 0);
        Assert.StartsWith("INSERT [Log]", log.ExecTrace, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("INSERT INTO", log.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Log_Error_Success()
    {
        var row= new Dictionary<string, object>
        {
            { "key", "1" },
            { "value", DateTime.Now }
        };
        try
        {
            await _db.Insertable(row).AS("unexist_table").ExecuteCommandAsync(TestContext.Current.CancellationToken);
        }
        catch
        {
            // do nothing
        }
        var log = await _db.Queryable<DbMonitorEntity>()
            .OrderByDescending(i => i.CreatedAt)
            .FirstAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(log);
        Assert.Equal("Error", log.LogLevel, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("exception", log.ExecTrace, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("INSERT INTO", log.Sql,  StringComparison.OrdinalIgnoreCase);
    }

    public async ValueTask DisposeAsync()
    {
        _sp.GetService<ISqlSugarClient>()?.Dispose();
        await (_sp.GetService<DbMonitor>()?.DisposeAsync() ?? ValueTask.CompletedTask);
        try
        {
            File.Delete("eveconv.db");
        }
        catch
        {
            // do nothing
        }
        GC.SuppressFinalize(this);
    }
}
