using MMCA.Common.Application.UseCases;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.ChangeStatus;

/// <summary>
/// Command to change a ticket's lifecycle status. Evicts cached ticket reads on success.
/// Carries the client's last-seen concurrency token (ADR-035), which the endpoint reads from the
/// <c>If-Match</c> header.
/// </summary>
public sealed record ChangeTicketStatusCommand(TicketIdentifierType TicketId, TicketStatus Status)
    : ICacheInvalidating
{
    /// <summary>
    /// The client's last-seen concurrency token (ADR-035). Required: the endpoint is conditional, so
    /// a request that states no precondition is refused with 428 before a command is ever built.
    /// </summary>
    public required byte[] RowVersion { get; init; }

    public string CachePrefix => TicketCacheKeys.Prefix;
}
