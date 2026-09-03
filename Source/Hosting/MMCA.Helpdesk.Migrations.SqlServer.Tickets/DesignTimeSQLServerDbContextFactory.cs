using Microsoft.EntityFrameworkCore.Design;
using MMCA.Common.Infrastructure.Persistence.DataSources;
using MMCA.Common.Infrastructure.Persistence.DbContexts;
using MMCA.Common.Infrastructure.Persistence.DbContexts.Design;

namespace MMCA.Helpdesk.Migrations.SqlServer.Tickets;

/// <summary>
/// Design-time factory for the Tickets database. Used by <c>dotnet ef migrations</c>; builds a
/// per-source context over the single Common <see cref="SQLServerDbContext"/> and discovers the
/// module's EF configurations from its Infrastructure assembly. The connection string is only used
/// by <c>migrations apply</c>/<c>database update</c>; <c>migrations add</c>/<c>script</c> never connect.
/// </summary>
/// <remarks>
/// The top-level connection string and the <c>Tickets</c> entry carry the SAME value on purpose. That
/// is exactly what the host injects for the <c>Tickets</c> data source at run time, and the resolver
/// collapses a logical name onto <c>Default</c> when the two connections match.
/// The scaffolded model therefore matches the running one, which matters for the framework tables that
/// are Default-source-only (<c>ScheduledJobs</c>). Point <c>HELPDESK_TICKETS_SQL</c> at a real database
/// to apply.
/// </remarks>
public sealed class DesignTimeSQLServerDbContextFactory : IDesignTimeDbContextFactory<SQLServerDbContext>
{
    public SQLServerDbContext CreateDbContext(string[] args) =>
        DesignTimeDbContextHelper.CreateSqlServer(args, options =>
        {
            // template:begin sqlserver
            var connectionString = Environment.GetEnvironmentVariable("HELPDESK_TICKETS_SQL")
                ?? "Server=localhost;Database=Helpdesk;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

            // template:end sqlserver
            options.DataSourceName = "Tickets";
            options.ConnectionStrings = new ConnectionStringSettings { SQLServerConnectionString = connectionString };
            options.DataSources["Tickets"] = new DataSourceEntrySettings
            {
                SQLServerConnectionString = connectionString,
                SQLServerMigrationsAssembly = typeof(DesignTimeSQLServerDbContextFactory).Assembly.GetName().Name!,
            };

            // Framework tables that only exist when the host opts in. Keep these in step with the host's
            // AuditTrail:Enabled / Scheduler:Enabled, or the scaffolded model drifts from the running one.
            // Tenancy needs no flag here: the TenantId column follows from ITenantEntity on the entities.
            options.EnableAuditTrail = true;
            options.EnableScheduler = true;
            options.AddConfigurationAssembly(typeof(MMCA.Helpdesk.Tickets.Infrastructure.AssemblyReference).Assembly);
        });
}
