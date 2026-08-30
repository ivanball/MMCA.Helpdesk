namespace MMCA.Helpdesk.Tickets.Shared.Tickets;

/// <summary>
/// Request body for editing a comment's body (the ticket id and comment id come from the route).
/// It carries no optimistic-concurrency token: the caller states the owning ticket's version in the
/// <c>If-Match</c> header (ADR-035). Concurrency is an aggregate-level invariant here, so an edit
/// conditioned on a stale ticket version is rejected rather than silently applied.
/// </summary>
/// <param name="Body">The new comment text.</param>
public sealed record EditCommentRequest(string Body);
