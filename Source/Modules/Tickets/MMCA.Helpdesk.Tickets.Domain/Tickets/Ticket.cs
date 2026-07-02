using MMCA.Common.Domain.Attributes;
using MMCA.Common.Domain.Entities;
using MMCA.Common.Domain.Enums;
using MMCA.Common.Domain.Extensions;
using MMCA.Common.Shared.Abstractions;
using MMCA.Helpdesk.Tickets.Domain.Tickets.DomainEvents;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Tickets.Domain.Tickets;

/// <summary>
/// Support-ticket aggregate root. Created through the <see cref="Create"/> factory (returns a
/// <see cref="Result{T}"/>), mutated through guarded methods that raise <see cref="TicketChanged"/>
/// domain events. Comments are growable children managed via <see cref="AddComment"/>.
/// </summary>
[IdValueGenerated]
public sealed class Ticket : AuditableAggregateRootEntity<TicketIdentifierType>
{
    public string Title { get; private set; }
    public string Description { get; private set; }
    public TicketStatus Status { get; private set; }

    // The user id of the requester (resolved from the Identity module once one is added).
    public int RequesterUserId { get; private set; }

    private readonly List<TicketComment> _comments = [];

    [Navigation(IsCollection = true)]
    public IReadOnlyCollection<TicketComment> Comments => _comments.AsReadOnly();

    private Ticket(string title, string description, int requesterUserId)
    {
        Title = title;
        Description = description;
        RequesterUserId = requesterUserId;
        Status = TicketStatus.Open;
    }

    public static Result<Ticket> Create(
        TicketIdentifierType? id,
        string title,
        string description,
        int requesterUserId)
    {
        var validation = Result.Combine(
            TicketInvariants.EnsureTitleIsValid(title, nameof(Create)),
            TicketInvariants.EnsureDescriptionIsValid(description, nameof(Create)));
        if (validation.IsFailure)
        {
            return Result.Failure<Ticket>(validation.Errors);
        }

        bool isIdValueGenerated = typeof(Ticket).IsIdValueGenerated;

        var ticket = new Ticket(title, description, requesterUserId)
        {
            Id = isIdValueGenerated ? default : id!.Value,
        };

        // No "Added" domain event here: the Id is database-generated (still 0 at this point), so an
        // event captured now would carry a meaningless id. Creation is signalled by the
        // TicketOpenedIntegrationEvent that CreateTicketHandler publishes AFTER the commit, with the
        // real id.
        return Result.Success(ticket);
    }

    public Result<TicketComment> AddComment(
        TicketCommentIdentifierType? id,
        string body,
        int authorUserId)
    {
        var validation = TicketInvariants.EnsureStatusAllowsComments(Status, nameof(AddComment));
        if (validation.IsFailure)
        {
            return Result.Failure<TicketComment>(validation.Errors);
        }

        var commentResult = TicketComment.Create(id, body, authorUserId);
        if (commentResult.IsFailure)
        {
            return Result.Failure<TicketComment>(commentResult.Errors);
        }

        var comment = commentResult.Value!;
        _comments.Add(comment);
        AddDomainEvent(new TicketChanged(DomainEntityState.Updated, Id));

        return Result.Success(comment);
    }

    public Result UpdateDetails(string title, string description)
    {
        var validation = Result.Combine(
            TicketInvariants.EnsureTitleIsValid(title, nameof(UpdateDetails)),
            TicketInvariants.EnsureDescriptionIsValid(description, nameof(UpdateDetails)));
        if (validation.IsFailure)
        {
            return validation;
        }

        Title = title;
        Description = description;
        AddDomainEvent(new TicketChanged(DomainEntityState.Updated, Id));

        return Result.Success();
    }

    public Result EditComment(TicketCommentIdentifierType commentId, string body)
    {
        var statusValidation = TicketInvariants.EnsureStatusAllowsComments(Status, nameof(EditComment));
        if (statusValidation.IsFailure)
        {
            return statusValidation;
        }

        var commentResult = GetCommentOrNotFound(commentId, nameof(EditComment));
        if (commentResult.IsFailure)
        {
            return Result.Failure(commentResult.Errors);
        }

        var editResult = commentResult.Value!.EditBody(body);
        if (editResult.IsFailure)
        {
            return editResult;
        }

        AddDomainEvent(new TicketChanged(DomainEntityState.Updated, Id));

        return Result.Success();
    }

    public Result RemoveComment(TicketCommentIdentifierType commentId)
    {
        var statusValidation = TicketInvariants.EnsureStatusAllowsComments(Status, nameof(RemoveComment));
        if (statusValidation.IsFailure)
        {
            return statusValidation;
        }

        var commentResult = GetCommentOrNotFound(commentId, nameof(RemoveComment));
        if (commentResult.IsFailure)
        {
            return Result.Failure(commentResult.Errors);
        }

        var deleteResult = commentResult.Value!.Delete();
        if (deleteResult.IsFailure)
        {
            return deleteResult;
        }

        AddDomainEvent(new TicketChanged(DomainEntityState.Updated, Id));

        return Result.Success();
    }

    public Result ChangeStatus(TicketStatus newStatus)
    {
        if (Status == newStatus)
        {
            return Result.Success();
        }

        Status = newStatus;
        AddDomainEvent(new TicketChanged(DomainEntityState.Updated, Id));

        return Result.Success();
    }

    public override Result Delete()
    {
        var result = base.Delete();
        if (result.IsFailure)
        {
            return result;
        }

        foreach (var comment in _comments.Where(c => !c.IsDeleted))
        {
            comment.Delete();
        }

        AddDomainEvent(new TicketChanged(DomainEntityState.Deleted, Id));

        return result;
    }

    private Result<TicketComment> GetCommentOrNotFound(TicketCommentIdentifierType commentId, string source)
    {
        var comment = _comments.FirstOrDefault(c => c.Id == commentId && !c.IsDeleted);
        return comment is null
            ? Result.Failure<TicketComment>(
                Error.NotFound.WithSource(source).WithTarget(nameof(TicketComment)))
            : Result.Success(comment);
    }
}
