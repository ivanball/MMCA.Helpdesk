using Microsoft.EntityFrameworkCore.Metadata;
using MMCA.Common.Infrastructure.Persistence.Conversions;
using MMCA.Common.Infrastructure.Persistence.DataSources;
using MMCA.Common.Infrastructure.Persistence.DbContexts.Design;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Architecture.Tests.Persistence;

/// <summary>
/// Pins the persistence half of the strongly typed child identifier (ADR-115): the key keeps the
/// <see langword="int"/> identity column it had before the wrapper, through the framework's wrapper converter,
/// so adopting the wrapper is not a migration.
/// <para>
/// The model is built the way <c>dotnet ef</c> builds it, through the same
/// <see cref="DesignTimeDbContextHelper"/> entry point the migrations project's design-time factory
/// uses, naming the same identifier assembly the host passes to <c>AddStronglyTypedIds</c>. No
/// database is involved: only the model is read, and no connection is ever made.
/// </para>
/// </summary>
public sealed class TicketCommentIdMappingTests
{
    /// <summary>A placeholder: the model is built offline, so no connection is ever made.</summary>
    private const string DesignTimeConnection = "unused";

    private readonly IModel _model = BuildModel();

    [Fact]
    public void CommentId_HasWrapperConverter_ToInt()
    {
        var key = IdProperty();

        key.GetValueConverter().Should().BeOfType<StronglyTypedIdValueConverter<TicketCommentId, int>>(
            "the design-time model must map the wrapper exactly as the runtime model does");
        key.GetValueConverter()!.ProviderClrType.Should().Be<int>(
            "the column stays the int it was before the wrapper existed");
    }

    [Fact]
    public void CommentId_IsStoreGenerated() =>
        IdProperty().ValueGenerated.Should().Be(
            ValueGenerated.OnAdd,
            "the child is [IdValueGenerated], so a wrapped key must keep the database-generated identity a bare int key had");

    private IProperty IdProperty() =>
        _model.FindEntityType(typeof(TicketComment))!.FindProperty(nameof(TicketComment.Id))!;

    /// <summary>
    /// Builds the module's finalized model with the settings <c>DeleteBehaviorConventionTests</c>
    /// uses, plus the identifier assembly: without it the wrapped key has no converter and the model
    /// does not build at all.
    /// </summary>
    /// <returns>The finalized EF model.</returns>
    private static IModel BuildModel()
    {
        using var context = DesignTimeDbContextHelper.CreateSqlServer([], options =>
        {
            options.DataSourceName = "Tickets";
            options.ConnectionStrings = new ConnectionStringSettings { SQLServerConnectionString = DesignTimeConnection };
            options.DataSources["Tickets"] = new DataSourceEntrySettings { SQLServerConnectionString = DesignTimeConnection };
            options.EnableAuditTrail = true;
            options.EnableScheduler = true;
            options.AddConfigurationAssembly(typeof(MMCA.Helpdesk.Tickets.Infrastructure.AssemblyReference).Assembly);
            options.AddStronglyTypedIdAssembly(typeof(TicketDTO).Assembly);
        });

        return context.Model;
    }
}
