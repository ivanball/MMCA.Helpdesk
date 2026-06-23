using Microsoft.EntityFrameworkCore.Design;
using MMCA.Common.Infrastructure.Persistence.DbContexts;
using MMCA.Common.Infrastructure.Persistence.DbContexts.Design;
using MMCA.Common.Infrastructure.Settings;

namespace MMCA.Helpdesk.Migrations.SqlServer.Tickets;

/// <summary>
/// Design-time factory for the Tickets database. Used by <c>dotnet ef migrations</c>; builds a
/// per-source context over the single Common <see cref="SQLServerDbContext"/> and discovers the
/// module's EF configurations from its Infrastructure assembly. The connection string is only used
/// by <c>migrations apply</c>/<c>database update</c>; <c>migrations add</c>/<c>script</c> never connect.
/// </summary>
public sealed class DesignTimeSQLServerDbContextFactory : IDesignTimeDbContextFactory<SQLServerDbContext>
{
    public SQLServerDbContext CreateDbContext(string[] args) =>
        DesignTimeDbContextHelper.CreateSqlServer(args, options =>
        {
            options.DataSourceName = "Tickets";
            options.ConnectionStrings = new ConnectionStringSettings { SQLServerConnectionString = "Server=design-time-unused;" };
            options.DataSources["Tickets"] = new DataSourceEntrySettings
            {
                SQLServerConnectionString = Environment.GetEnvironmentVariable("HELPDESK_TICKETS_SQL")
                    ?? "Server=localhost;Database=Helpdesk_Tickets;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True",
                SQLServerMigrationsAssembly = typeof(DesignTimeSQLServerDbContextFactory).Assembly.GetName().Name!,
            };
            options.AddConfigurationAssembly(typeof(MMCA.Helpdesk.Tickets.Infrastructure.AssemblyReference).Assembly);
        });
}
