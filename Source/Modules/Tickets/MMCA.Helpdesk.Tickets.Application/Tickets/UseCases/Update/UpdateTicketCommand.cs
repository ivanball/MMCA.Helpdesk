using MMCA.Common.Application.UseCases;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Update;

/// <summary>
/// Command to update a ticket's editable details. Evicts cached ticket reads on success.
/// Carries the client's last-seen concurrency token (ADR-035); null skips the conflict check.
/// </summary>
/// <remarks>
/// The positional parameter list stays on ONE line on purpose: its last member is an optional axis
/// of the scaffold, and a positional record cannot carry a trailing comma, so a whole-line marker
/// region could not drop it. build/templates/stage.ps1 rewrites this line per shape.
/// </remarks>
public sealed record UpdateTicketCommand(TicketIdentifierType TicketId, string Title, string Description)
    : ICacheInvalidating
{
    /// <summary>The client's last-seen concurrency token; null skips the conflict check (ADR-035).</summary>
    public byte[]? RowVersion { get; init; }

    public string CachePrefix => TicketCacheKeys.Prefix;
}
