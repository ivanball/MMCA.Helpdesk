using MMCA.Common.Application.UseCases;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.EditComment;

/// <summary>
/// Command to edit the body of an existing comment on a ticket. Carries the client's last-seen
/// concurrency token for the owning ticket (ADR-035); null skips the conflict check.
/// </summary>
public sealed record EditCommentCommand(
    TicketIdentifierType TicketId,
    TicketCommentIdentifierType CommentId,
    string Body) : ICacheInvalidating
{
    /// <summary>
    /// The client's last-seen concurrency token for the owning ticket; null skips the conflict
    /// check (ADR-035). Concurrency is stamped on the aggregate root, which is also what the
    /// ticket read's <c>ETag</c> carries.
    /// </summary>
    public byte[]? RowVersion { get; init; }

    public string CachePrefix => TicketCacheKeys.Prefix;
}
