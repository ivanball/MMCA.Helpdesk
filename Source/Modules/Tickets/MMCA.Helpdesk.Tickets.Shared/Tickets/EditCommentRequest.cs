using MMCA.Common.Shared.DTOs;

namespace MMCA.Helpdesk.Tickets.Shared.Tickets;

/// <summary>
/// Request body for editing a comment's body (the ticket id and comment id come from the route).
/// Round-trips the optimistic-concurrency token per ADR-035. The token is the owning ticket's
/// <see cref="RowVersion"/> (the one the ticket read returns, and the one the read's <c>ETag</c>
/// carries): concurrency is an aggregate-level invariant here, so an edit conditioned on a stale
/// ticket version is rejected rather than silently applied.
/// </summary>
/// <param name="Body">The new comment text.</param>
public sealed record EditCommentRequest(string Body) : IConcurrencyAware
{
    /// <inheritdoc />
    public byte[]? RowVersion { get; init; }
}
