using MMCA.Common.Infrastructure.Persistence.DataSources;
using MMCA.Common.Infrastructure.Persistence.DbContexts.Design;

namespace MMCA.Helpdesk.Architecture.Tests;

/// <summary>
/// Delete-behavior fitness functions over this app's finalized EF model: a foreign key that cascades
/// must carry <c>MMCA:DeleteBehaviorSource = Explicit</c> (somebody chose it in an entity
/// configuration), and every foreign key the framework's <c>RestrictDeleteByDefaultConvention</c>
/// stamped <c>Convention</c> must restrict.
/// <para>
/// The model is built the way <c>dotnet ef</c> builds it, through the same
/// <see cref="DesignTimeDbContextHelper"/> entry point the migrations project's design-time factory
/// uses, so the schema this asserts is the schema the next scaffolded migration describes. No
/// database is involved: only the model is read, and the connection is never opened. The two
/// framework-table flags mirror the design-time factory and the host settings, and must stay in step
/// with both.
/// </para>
/// </summary>
public sealed class DeleteBehaviorConventionTests : DeleteBehaviorConventionTestsBase
{
    /// <summary>A placeholder: the model is built offline, so no connection is ever opened.</summary>
    private const string DesignTimeConnection = "unused";

    /// <inheritdoc />
    protected override object Model { get; } = BuildModel();

    /// <summary>
    /// Builds the module's finalized model. The top-level connection and the <c>Tickets</c> entry
    /// carry the same value on purpose, exactly as the design-time factory declares them, so the
    /// resolver collapses the logical source onto <c>Default</c> and the Default-source-only
    /// framework tables are part of the model that is asserted.
    /// </summary>
    /// <returns>The finalized EF model.</returns>
    private static object BuildModel()
    {
        using var context = DesignTimeDbContextHelper.CreateSqlServer([], options =>
        {
            options.DataSourceName = "Tickets";
            options.ConnectionStrings = new ConnectionStringSettings { SQLServerConnectionString = DesignTimeConnection };
            options.DataSources["Tickets"] = new DataSourceEntrySettings { SQLServerConnectionString = DesignTimeConnection };
            options.EnableAuditTrail = true;
            options.EnableScheduler = true;
            options.AddConfigurationAssembly(typeof(MMCA.Helpdesk.Tickets.Infrastructure.AssemblyReference).Assembly);
        });

        return context.Model;
    }
}
