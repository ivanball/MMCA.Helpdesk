using MMCA.Common.Shared.DTOs;

namespace MMCA.Helpdesk.Tickets.Shared.Tickets;

/// <summary>
/// Request body for updating a ticket's title and description (the ticket id comes from the route).
/// Round-trips the optimistic-concurrency token per ADR-035: the client echoes the
/// <see cref="RowVersion"/> it last read so a conflicting concurrent edit surfaces as 409 instead
/// of silently last-write-winning. Named with the <c>*UpdateRequest</c> suffix so the shared
/// <c>UpdateRequestsAreConcurrencyAware</c> fitness rule covers it.
/// </summary>
public sealed record class TicketUpdateRequest : IConcurrencyAware
{
    /// <inheritdoc />
    public byte[]? RowVersion { get; init; }

    /// <summary>The new title.</summary>
    public required string Title { get; init; }

    /// <summary>The new description.</summary>
    public required string Description { get; init; }
}
