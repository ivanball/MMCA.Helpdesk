using MMCA.Common.Application.UseCases;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Update;

/// <summary>
/// Command to update a ticket's title and description. Evicts cached ticket reads on success.
/// </summary>
public sealed record UpdateTicketCommand(TicketIdentifierType TicketId, string Title, string Description)
    : ICacheInvalidating
{
    public string CachePrefix => $"{typeof(Ticket).FullName}:";
}
