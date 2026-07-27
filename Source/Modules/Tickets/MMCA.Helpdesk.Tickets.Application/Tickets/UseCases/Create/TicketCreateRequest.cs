using MMCA.Common.Application.Interfaces;
using MMCA.Common.Application.UseCases;

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
    public required string Description { get; init; }
    public required int RequesterUserId { get; init; }
}
