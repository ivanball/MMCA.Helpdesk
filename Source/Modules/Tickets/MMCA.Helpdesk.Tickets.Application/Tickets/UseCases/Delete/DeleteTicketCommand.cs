using MMCA.Common.Application.UseCases;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Delete;

/// <summary>
/// Command to soft-delete a ticket (and cascade-soft-delete its comments). Evicts cached reads on success.
/// </summary>
public sealed record DeleteTicketCommand(TicketIdentifierType TicketId) : ICacheInvalidating
{
    public string CachePrefix => $"{typeof(Ticket).FullName}:";
}
