using MMCA.Common.Domain.DomainEvents;

namespace MMCA.Helpdesk.Tickets.Shared.Tickets.IntegrationEvents;

/// <summary>
/// Raised when a ticket is opened. Lives in the Shared layer so other modules (or extracted
/// services) can consume it without referencing Tickets.Domain. Carries the framework
/// <see cref="BaseIntegrationEvent.SchemaVersion"/> (default 1, ADR-010): a breaking change uses a
/// new event type plus an upcaster, never a silent reshape of this contract.
/// </summary>
/// <param name="TicketId">The newly opened ticket's database-generated identifier.</param>
/// <param name="RequesterUserId">The user who opened the ticket.</param>
public sealed record class TicketOpenedIntegrationEvent(
    TicketIdentifierType TicketId,
    int RequesterUserId)
    : BaseIntegrationEvent;
