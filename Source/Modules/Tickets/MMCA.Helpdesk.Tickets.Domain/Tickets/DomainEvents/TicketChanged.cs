using MMCA.Common.Domain.DomainEvents;
using MMCA.Common.Domain.Enums;

namespace MMCA.Helpdesk.Tickets.Domain.Tickets.DomainEvents;

/// <summary>
/// Domain event raised when a <c>Ticket</c> is opened, mutated, or deleted. Captured into the
/// outbox by <c>ApplicationDbContext.SaveChangesAsync</c> and dispatched after commit.
/// </summary>
/// <param name="State">The lifecycle state change (Added, Updated, or Deleted).</param>
/// <param name="TicketId">The affected ticket's identifier.</param>
public sealed record class TicketChanged(
    DomainEntityState State,
    TicketIdentifierType TicketId)
    : EntityChangedEvent<TicketIdentifierType>(State, TicketId);
