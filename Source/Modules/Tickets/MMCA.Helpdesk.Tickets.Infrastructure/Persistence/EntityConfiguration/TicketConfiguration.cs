// template:begin child
// One call below (a navigation access mode) reaches into this namespace; every other call is an
// instance method on a builder from Metadata.Builders.
using Microsoft.EntityFrameworkCore;
// template:end child
using Microsoft.EntityFrameworkCore.Metadata.Builders;
// template:begin owner
// The filtered index below is engine-aware: DataSource names the engine, and HasSoftDeleteFilter
// lives one namespace above the configuration bases.
using MMCA.Common.Application.Interfaces.Infrastructure.Persistence;
using MMCA.Common.Infrastructure.Persistence.Configuration;
// template:end owner
using MMCA.Common.Infrastructure.Persistence.Configuration.EntityTypeConfiguration;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Tickets.Infrastructure.Persistence.EntityConfiguration;

/// <summary>
/// EF Core configuration for the <see cref="Ticket"/> aggregate. <c>base.Configure</c> wires the Id,
/// soft-delete flag + query filter, audit fields, and concurrency token from the framework base.
/// </summary>
internal sealed class TicketConfiguration : EntityTypeConfigurationSQLServer<Ticket, TicketIdentifierType>
{
    public override void Configure(EntityTypeBuilder<Ticket> builder)
    {
        base.Configure(builder);

        builder.Property(p => p.Title)
            .HasMaxLength(TicketInvariants.TitleMaxLength)
            .IsRequired();

        // template:begin description
        builder.Property(p => p.Description)
            .HasMaxLength(TicketInvariants.DescriptionMaxLength)
            .IsRequired();

        // template:end description
        // template:begin status
        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // template:end status
        // template:begin owner
        builder.Property(p => p.RequesterUserId)
            .IsRequired();

        // Filtered on live rows only: soft-deleted rows are hidden by the global query filter but
        // still occupy index pages. HasSoftDeleteFilter rather than a HasFilter literal because the
        // predicate is not the same string on every engine: the column is quoted the provider's way,
        // and on PostgreSQL the flag is a real boolean, so comparing it with 0 is a type error the
        // server refuses at CREATE INDEX. The engine argument is the same token the configuration
        // base carries, so scaffolding rewrites both together.
        builder.HasIndex(p => p.RequesterUserId)
            .HasSoftDeleteFilter(DataSource.SQLServer);

        // template:end owner
        // template:begin child
        builder.HasMany(p => p.Comments)
            .WithOne(c => c.Ticket)
            .HasForeignKey(c => c.TicketId)
            .IsRequired();

        // The child collection is a read-only wrapper over a backing field, so EF must read and
        // materialize through the field rather than the property.
        builder.Navigation(p => p.Comments)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        // template:end child
    }
}
