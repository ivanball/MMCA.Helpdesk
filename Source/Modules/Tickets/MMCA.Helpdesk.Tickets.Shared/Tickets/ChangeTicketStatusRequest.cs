namespace MMCA.Helpdesk.Tickets.Shared.Tickets;

/// <summary>
/// Request body for changing a ticket's status (the ticket id comes from the route).
/// </summary>
/// <param name="Status">The new status.</param>
public sealed record ChangeTicketStatusRequest(TicketStatus Status);
