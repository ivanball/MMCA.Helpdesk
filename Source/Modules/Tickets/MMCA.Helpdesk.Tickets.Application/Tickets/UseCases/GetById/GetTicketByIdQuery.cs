namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.GetById;

/// <summary>
/// Query for a single ticket including its (non-deleted) comments.
/// </summary>
public sealed record GetTicketByIdQuery(TicketIdentifierType Id);
