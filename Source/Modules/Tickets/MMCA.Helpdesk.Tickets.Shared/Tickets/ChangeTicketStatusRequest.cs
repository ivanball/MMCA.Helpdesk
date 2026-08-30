namespace MMCA.Helpdesk.Tickets.Shared.Tickets;

/// <summary>
/// Request body for changing a ticket's status (the ticket id comes from the route).
/// It carries no optimistic-concurrency token: the caller states the version it last read in the
/// <c>If-Match</c> header (the <c>ETag</c> the read returned), and a conflicting concurrent edit
/// surfaces as 412 rather than silently last-write-winning (ADR-035).
/// </summary>
/// <param name="Status">The new status.</param>
public sealed record ChangeTicketStatusRequest(TicketStatus Status);
