using MMCA.Common.Application.UseCases;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.EditComment;

/// <summary>
/// Command to edit the body of an existing comment on a ticket.
/// </summary>
public sealed record EditCommentCommand(
    TicketIdentifierType TicketId,
    TicketCommentIdentifierType CommentId,
    string Body) : ICacheInvalidating
{
    public string CachePrefix => $"{typeof(Ticket).FullName}:";
}
