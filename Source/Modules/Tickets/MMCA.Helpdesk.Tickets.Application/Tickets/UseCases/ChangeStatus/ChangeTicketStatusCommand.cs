using MMCA.Common.Application.UseCases;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.ChangeStatus;

/// <summary>
/// Command to change a ticket's lifecycle status. Evicts cached ticket reads on success.
/// Carries the client's last-seen concurrency token (ADR-035); null skips the conflict check.
/// </summary>
public sealed record ChangeTicketStatusCommand(TicketIdentifierType TicketId, TicketStatus Status)
    : ICacheInvalidating
{
    /// <summary>The client's last-seen concurrency token; null skips the conflict check (ADR-035).</summary>
    public byte[]? RowVersion { get; init; }

    public string CachePrefix => TicketCacheKeys.Prefix;
}
