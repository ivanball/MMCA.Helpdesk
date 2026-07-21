using MMCA.Common.Application.UseCases;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Update;

/// <summary>
/// Command to update a ticket's title and description. Evicts cached ticket reads on success.
/// Carries the client's last-seen concurrency token (ADR-035); null skips the conflict check.
/// </summary>
public sealed record UpdateTicketCommand(
    TicketIdentifierType TicketId,
    string Title,
    string Description)
    : ICacheInvalidating
{
    /// <summary>The client's last-seen concurrency token; null skips the conflict check (ADR-035).</summary>
    public byte[]? RowVersion { get; init; }

    public string CachePrefix => $"{typeof(Ticket).FullName}:";
}
