using Microsoft.EntityFrameworkCore;
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

        builder.Property(p => p.Description)
            .HasMaxLength(TicketInvariants.DescriptionMaxLength)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(p => p.RequesterUserId)
            .IsRequired();

        builder.HasIndex(p => p.RequesterUserId)
            .HasFilter("[IsDeleted] = 0");

        builder.HasMany(p => p.Comments)
            .WithOne(c => c.Ticket)
            .HasForeignKey(c => c.TicketId)
            .IsRequired();
    }
}
