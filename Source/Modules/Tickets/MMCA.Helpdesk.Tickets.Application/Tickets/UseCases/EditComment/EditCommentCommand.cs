using MMCA.Common.Application.UseCases;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.EditComment;

/// <summary>
/// Command to edit the body of an existing comment on a ticket. Carries the client's last-seen
/// concurrency token for the owning ticket (ADR-035), which the endpoint reads from the
/// <c>If-Match</c> header.
/// </summary>
public sealed record EditCommentCommand(
    TicketIdentifierType TicketId,
    TicketCommentIdentifierType CommentId,
    string Body) : ICacheInvalidating
{
    /// <summary>
    /// The client's last-seen concurrency token for the owning ticket (ADR-035). Concurrency is
    /// stamped on the aggregate root, which is also what the ticket read's <c>ETag</c> carries.
    /// Required: the endpoint is conditional, so a request that states no precondition is refused
    /// with 428 before a command is ever built.
    /// </summary>
    public required byte[] RowVersion { get; init; }

    public string CachePrefix => TicketCacheKeys.Prefix;
}
