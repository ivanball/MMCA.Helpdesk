using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMCA.Common.Infrastructure.Persistence.Configuration.EntityTypeConfiguration;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Tickets.Infrastructure.Persistence.EntityConfiguration;

/// <summary>
/// EF Core configuration for the <see cref="TicketComment"/> child entity. The parent
/// <see cref="TicketConfiguration"/> owns the relationship; this configures the comment's own columns.
/// </summary>
internal sealed class TicketCommentConfiguration
    : EntityTypeConfigurationSQLServer<TicketComment, TicketCommentIdentifierType>
{
    public override void Configure(EntityTypeBuilder<TicketComment> builder)
    {
        base.Configure(builder);

        builder.Property(p => p.Body)
            .HasMaxLength(TicketInvariants.CommentBodyMaxLength)
            .IsRequired();

        builder.Property(p => p.AuthorUserId)
            .IsRequired();
    }
}
