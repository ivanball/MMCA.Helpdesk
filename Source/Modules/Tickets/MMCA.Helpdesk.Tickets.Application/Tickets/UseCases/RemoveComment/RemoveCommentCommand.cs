using MMCA.Common.Application.UseCases.Markers;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.RemoveComment;

/// <summary>
/// Command to remove (soft-delete) a comment from a ticket.
/// </summary>
public sealed record RemoveCommentCommand(
    TicketIdentifierType TicketId,
    TicketCommentIdentifierType CommentId) : ICacheInvalidating<Ticket>;
