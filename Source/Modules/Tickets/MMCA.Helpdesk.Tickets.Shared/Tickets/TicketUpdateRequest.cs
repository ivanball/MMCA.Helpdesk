namespace MMCA.Helpdesk.Tickets.Shared.Tickets;

/// <summary>
/// Request body for updating a ticket's editable details (the ticket id comes from the route).
/// It carries no optimistic-concurrency token: the precondition travels in the <c>If-Match</c>
/// header alone (ADR-035), where <c>[SupportsIfMatch]</c> reads it, so a request that states no
/// precondition is refused with 428 instead of silently last-write-winning. The
/// <c>*UpdateRequest</c> suffix is what puts this type under the shared
/// <c>UpdateRequests_ShouldNotImplement_IConcurrencyAware</c> fitness rule.
/// </summary>
public sealed record class TicketUpdateRequest
{
    /// <summary>The new title.</summary>
    public required string Title { get; init; }

    // template:begin description
    /// <summary>The new description.</summary>
    public required string Description { get; init; }
    // template:end description
}
