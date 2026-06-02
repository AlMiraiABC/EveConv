using EveConv.RdbStorage.Monitor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace EveConv.RdbStorage;

public static class EveConvDbContextExtensions
{
    private const string DefaultConnectionStringName = "Data Source=eveconv.db";

    public static IServiceCollection AddDbContext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<ISqlSugarClient>(s =>
        {
            var options = s.GetRequiredService<IOptions<EveConvDbContextOptions>>();
            var loggerFactory = s.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger(nameof(RdbStorage)) ?? NullLogger.Instance;
            var connectionString = configuration.GetConnectionString("Storage");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = DefaultConnectionStringName;
            }
            var connectionConfig = new ConnectionConfig()
            {
                DbType = options.Value.DbType,
                ConnectionString = connectionString,
                IsAutoCloseConnection = true,
            };
            var dbMonitor = s.GetService<IDbMonitor>() ?? new NoopDbMonitor(loggerFactory);
            return new SqlSugarScope(connectionConfig,
                db =>
                {
                    db.Aop.OnLogExecuted = (sql, ps) =>
                    {
                        if (options.Value.SlowExecThreshold <= TimeSpan.Zero)
                        {
                            return;
                        }
                        var execTime = db.Ado.SqlExecutionTime;
                        if (execTime < options.Value.SlowExecThreshold)
                        {
                            return;
                        }
                        dbMonitor.Log(sql, db.Ado);
                    };
                    db.Aop.OnError = ex => { dbMonitor.Log(ex); };
                }
            );
        });
        return services;
    }

    public static IServiceCollection AddDbMonitor(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddSingleton<IDbMonitor>(s =>
        {
            var options = s.GetRequiredService<IOptions<EveConvDbContextOptions>>();
            var loggerFactory = s.GetService<ILoggerFactory>();
            var connectionString = configuration.GetConnectionString("Storage");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = DefaultConnectionStringName;
            }
            var connectionConfig = new ConnectionConfig()
            {
                DbType = options.Value.DbType,
                ConnectionString = connectionString,
                IsAutoCloseConnection = true,
            };
            return new DbMonitor(new SqlSugarClient(connectionConfig), options.Value.LogMaxQueueSize, loggerFactory);
        });
        return services;
    }
}
