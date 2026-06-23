namespace MMCA.Helpdesk.Tickets.Shared.Tickets;

/// <summary>
/// Request body for updating a ticket's title and description (the ticket id comes from the route).
/// </summary>
/// <param name="Title">The new title.</param>
/// <param name="Description">The new description.</param>
public sealed record UpdateTicketRequest(string Title, string Description);
