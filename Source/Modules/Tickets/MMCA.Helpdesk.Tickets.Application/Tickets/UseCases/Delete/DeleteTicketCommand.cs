using MMCA.Common.Application.UseCases.Markers;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Delete;

/// <summary>
/// Command to soft-delete a ticket (and cascade-soft-delete its children). Evicts cached reads on success.
/// </summary>
public sealed record DeleteTicketCommand(TicketIdentifierType TicketId) : ICacheInvalidating<Ticket>;
