using MMCA.Common.Domain.Attributes;
using MMCA.Common.Domain.Entities;
using MMCA.Common.Domain.Extensions;
using MMCA.Common.Shared.Abstractions;

namespace MMCA.Helpdesk.Tickets.Domain.Tickets;

/// <summary>
/// A comment on a <see cref="Ticket"/>. Child entity of the Ticket aggregate; created and managed
/// through the aggregate root, never directly.
/// </summary>
[IdValueGenerated]
public sealed class TicketComment : AuditableBaseEntity<TicketCommentIdentifierType>
{
    [Navigation]
    public Ticket? Ticket { get; set; }

    public TicketIdentifierType TicketId { get; init; }

    public string Body { get; private set; }

    // The user id of the comment author (resolved from the Identity module once one is added).
    public int AuthorUserId { get; private set; }

    private TicketComment(string body, int authorUserId)
    {
        Body = body;
        AuthorUserId = authorUserId;
    }

    public static Result<TicketComment> Create(
        TicketCommentIdentifierType? id,
        string body,
        int authorUserId)
    {
        var validation = TicketInvariants.EnsureCommentBodyIsValid(body, nameof(Create));
        if (validation.IsFailure)
        {
            return Result.Failure<TicketComment>(validation.Errors);
        }

        bool isIdValueGenerated = typeof(TicketComment).IsIdValueGenerated;

        var comment = new TicketComment(body, authorUserId)
        {
            Id = isIdValueGenerated ? default : id!.Value,
        };

        return Result.Success(comment);
    }

    public Result EditBody(string body)
    {
        var validation = TicketInvariants.EnsureCommentBodyIsValid(body, nameof(EditBody));
        if (validation.IsFailure)
        {
            return validation;
        }

        Body = body;

        return Result.Success();
    }
}
