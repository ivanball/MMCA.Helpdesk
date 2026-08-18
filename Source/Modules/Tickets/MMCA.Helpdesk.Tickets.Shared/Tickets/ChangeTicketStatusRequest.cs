using MMCA.Common.Shared.DTOs;

namespace MMCA.Helpdesk.Tickets.Shared.Tickets;

/// <summary>
/// Request body for changing a ticket's status (the ticket id comes from the route).
/// Round-trips the optimistic-concurrency token per ADR-035: the client echoes the
/// <see cref="RowVersion"/> it last read (from the ticket DTO, or from the read's <c>ETag</c> via
/// <c>If-Match</c>) so a conflicting concurrent edit surfaces as 409 (412 on the header path)
/// instead of silently last-write-winning.
/// </summary>
/// <param name="Status">The new status.</param>
public sealed record ChangeTicketStatusRequest(TicketStatus Status) : IConcurrencyAware
{
    /// <inheritdoc />
    public byte[]? RowVersion { get; init; }
}
