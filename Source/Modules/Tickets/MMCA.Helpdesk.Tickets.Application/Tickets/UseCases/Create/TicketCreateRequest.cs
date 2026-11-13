using MMCA.Common.Application.Interfaces.Mapping;
using MMCA.Common.Application.UseCases.Markers;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Create;

/// <summary>
/// Command/request to open a new ticket. Used directly as the command (validated by the pipeline's
/// Validating decorator via <see cref="TicketCreateRequestValidator"/>); implements
/// <see cref="ICacheInvalidating{TEntity}"/> over <see cref="Ticket"/> so cached ticket reads are
/// evicted after a successful create.
/// </summary>
public record class TicketCreateRequest : ICreateRequest, ICacheInvalidating<Ticket>
{
    public required string Title { get; init; }
    // template:begin description
    public required string Description { get; init; }
    // template:end description
    // template:begin owner
    public required int RequesterUserId { get; init; }
    // template:end owner
}
