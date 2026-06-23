using MMCA.Common.Application.UseCases;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.AddComment;

/// <summary>
/// Command to append a comment to an existing ticket. Implements <see cref="ICacheInvalidating"/>
/// so cached ticket reads are evicted after a successful add.
/// </summary>
public sealed record AddCommentCommand(TicketIdentifierType TicketId, string Body, int AuthorUserId)
    : ICacheInvalidating
{
    public string CachePrefix => $"{typeof(Ticket).FullName}:";
}
