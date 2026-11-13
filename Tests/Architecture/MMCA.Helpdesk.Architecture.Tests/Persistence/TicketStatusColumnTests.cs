using Microsoft.EntityFrameworkCore.Metadata;
using MMCA.Common.Infrastructure.Persistence.Conversions;
using MMCA.Common.Infrastructure.Persistence.DataSources;
using MMCA.Common.Infrastructure.Persistence.DbContexts.Design;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Architecture.Tests.Persistence;

/// <summary>
/// Pins the status column across the move from the wire enum to the domain enumeration
/// <see cref="TicketLifecycle"/>: the column stays a 32-character string holding the member NAME, so
/// existing rows load unchanged and no migration is needed. The framework's other enumeration
/// converter stores the integer value instead, which would be a column type change with a data
/// rewrite.
/// <para>
/// The model is built the way <c>dotnet ef</c> builds it, through the same
/// <see cref="DesignTimeDbContextHelper"/> entry point the migrations project's design-time factory
/// uses. No database is involved: only the model is read, and no connection is ever made.
/// </para>
/// </summary>
public sealed class TicketStatusColumnTests
{
    /// <summary>A placeholder: the model is built offline, so no connection is ever made.</summary>
    private const string DesignTimeConnection = "unused";

    private readonly IModel _model = BuildModel();

    [Fact]
    public void Status_ProviderTypeIsString_MaxLength32()
    {
        var status = StatusProperty();

        status.GetValueConverter()!.ProviderClrType.Should().Be<string>(
            "the column holds the member name, as it did when the property was the enum");
        status.GetMaxLength().Should().Be(32);
        status.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void Status_UsesEnumerationNameValueConverter()
    {
        var converter = StatusProperty().GetValueConverter();

        converter.Should().BeOfType<EnumerationNameValueConverter<TicketLifecycle>>(
            "the int converter would change the column type and rewrite every stored row");
        converter.ConvertToProvider(TicketLifecycle.Resolved).Should().Be("Resolved");
        converter.ConvertFromProvider("InProgress").Should().Be(TicketLifecycle.InProgress);
    }

    private IProperty StatusProperty() =>
        _model.FindEntityType(typeof(Ticket))!.FindProperty(nameof(Ticket.Status))!;

    /// <summary>
    /// Builds the module's finalized model with the settings <c>DeleteBehaviorConventionTests</c>
    /// uses, plus the identifier assembly the host registers, so the model builds whichever optional
    /// axes the module carries.
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
