// template:begin childOrOwner
// A couple of calls below (a relational index filter, a navigation access mode) reach into this
// namespace; every other call is an instance method on a builder from Metadata.Builders.
using Microsoft.EntityFrameworkCore;
// template:end childOrOwner
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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

        builder.HasIndex(p => p.RequesterUserId)
            .HasFilter("[IsDeleted] = 0");

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
