using MMCA.Common.Application.UseCases;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.ChangeStatus;

/// <summary>
/// Command to change a ticket's lifecycle status. Evicts cached ticket reads on success.
/// </summary>
public sealed record ChangeTicketStatusCommand(TicketIdentifierType TicketId, TicketStatus Status)
    : ICacheInvalidating
{
    public string CachePrefix => $"{typeof(Ticket).FullName}:";
}
