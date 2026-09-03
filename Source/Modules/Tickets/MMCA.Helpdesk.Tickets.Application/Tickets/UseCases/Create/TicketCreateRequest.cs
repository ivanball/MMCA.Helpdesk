using MMCA.Common.Application.Interfaces.Mapping;
using MMCA.Common.Application.UseCases.Markers;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Create;

/// <summary>
/// Command/request to open a new ticket. Used directly as the command (validated by the pipeline's
/// Validating decorator via <see cref="TicketCreateRequestValidator"/>); implements
/// <see cref="ICacheInvalidating"/> so cached ticket reads are evicted after a successful create.
/// </summary>
public record class TicketCreateRequest : ICreateRequest, ICacheInvalidating
{
    public string CachePrefix => TicketCacheKeys.Prefix;

    public required string Title { get; init; }
    // template:begin description
    public required string Description { get; init; }
    // template:end description
    // template:begin owner
    public required int RequesterUserId { get; init; }
    // template:end owner
}
