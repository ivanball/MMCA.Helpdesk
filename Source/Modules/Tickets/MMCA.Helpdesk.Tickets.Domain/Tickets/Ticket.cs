using MMCA.Common.Domain.Attributes;
using MMCA.Common.Domain.Entities;
using MMCA.Common.Domain.Enums;
using MMCA.Common.Domain.Extensions;
using MMCA.Common.Domain.Interfaces;
using MMCA.Common.Shared.Abstractions;
using MMCA.Helpdesk.Tickets.Domain.Tickets.DomainEvents;
// template:begin status
using MMCA.Helpdesk.Tickets.Shared.Tickets;

// template:end status
namespace MMCA.Helpdesk.Tickets.Domain.Tickets;

/// <summary>
/// Support-ticket aggregate root. Created through the <see cref="Create"/> factory (returns a
/// <see cref="Result{T}"/>), mutated through guarded methods that raise <see cref="TicketChanged"/>
/// domain events.
/// template:begin child
/// Comments are growable children managed via <see cref="AddComment"/>.
/// template:end child
/// Marked <see cref="IAuditedEntity"/> (field-level change history) and <see cref="ITenantEntity"/>
/// (per-tenant row isolation): both are framework markers, opt-in per host through
/// <c>AddAuditTrail</c> / <c>AddMultiTenancy</c>, and inert when those are not configured.
/// </summary>
[IdValueGenerated]
public sealed class Ticket : AuditableAggregateRootEntity<TicketIdentifierType>, IAuditedEntity, ITenantEntity
{
    public string Title { get; private set; }

    // Owning tenant. Written by the framework's tenant interceptor on insert from the tenant resolved
    // for the request (claim, then X-Tenant-Id header); domain code never assigns it.
    public string TenantId { get; private set; } = string.Empty;
    // template:begin description
    public string Description { get; private set; }
    // template:end description
    // template:begin status
    public TicketStatus Status { get; private set; }
    // template:end status

    // template:begin owner
    // The user id of the requester (resolved from the Identity module once one is added).
    public int RequesterUserId { get; private set; }

    // template:end owner
    // template:begin child
    private readonly List<TicketComment> _comments = [];

    [Navigation(IsCollection = true)]
    public IReadOnlyCollection<TicketComment> Comments => _comments.AsReadOnly();

    // template:end child
    private Ticket(string title, string description, int requesterUserId)
    {
        Title = title;
        // template:begin description
        Description = description;
        // template:end description
        // template:begin owner
        RequesterUserId = requesterUserId;
        // template:end owner
        // template:begin status
        Status = TicketStatus.Open;
        // template:end status
    }

    public static Result<Ticket> Create(TicketIdentifierType? id, string title, string description, int requesterUserId)
    {
        var validation = Result.Combine(TicketInvariants.EnsureTitleIsValid(title, nameof(Create)), TicketInvariants.EnsureDescriptionIsValid(description, nameof(Create)));
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

    // template:begin child
    public Result<TicketComment> AddComment(
        TicketCommentIdentifierType? id,
        string body,
        int authorUserId)
    {
        // template:begin status
        var validation = TicketInvariants.EnsureStatusAllowsComments(Status, nameof(AddComment));
        if (validation.IsFailure)
        {
            return Result.Failure<TicketComment>(validation.Errors);
        }

        // template:end status
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

    // template:end child
    public Result UpdateDetails(string title, string description)
    {
        var validation = Result.Combine(TicketInvariants.EnsureTitleIsValid(title, nameof(UpdateDetails)), TicketInvariants.EnsureDescriptionIsValid(description, nameof(UpdateDetails)));
        if (validation.IsFailure)
        {
            return validation;
        }

        Title = title;
        // template:begin description
        Description = description;
        // template:end description
        AddDomainEvent(new TicketChanged(DomainEntityState.Updated, Id));

        return Result.Success();
    }

    // template:begin child
    public Result EditComment(TicketCommentIdentifierType commentId, string body)
    {
        // template:begin status
        var statusValidation = TicketInvariants.EnsureStatusAllowsComments(Status, nameof(EditComment));
        if (statusValidation.IsFailure)
        {
            return statusValidation;
        }

        // template:end status
        var commentResult = GetChildOrNotFound(_comments, commentId, nameof(EditComment));
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
        // template:begin status
        var statusValidation = TicketInvariants.EnsureStatusAllowsComments(Status, nameof(RemoveComment));
        if (statusValidation.IsFailure)
        {
            return statusValidation;
        }

        // template:end status
        var commentResult = GetChildOrNotFound(_comments, commentId, nameof(RemoveComment));
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

    // template:end child
    // template:begin status
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

    // template:end status
    public override Result Delete()
    {
        var result = base.Delete();
        if (result.IsFailure)
        {
            return result;
        }

        // template:begin child
        foreach (var comment in _comments.Where(c => !c.IsDeleted))
        {
            comment.Delete();
        }

        // template:end child
        AddDomainEvent(new TicketChanged(DomainEntityState.Deleted, Id));

        return result;
    }
}
