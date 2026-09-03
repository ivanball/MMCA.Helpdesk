using MMCA.Common.Application.Interfaces.Infrastructure.Persistence;
using MMCA.Common.Application.UseCases.Crud;
using MMCA.Common.Shared.Abstractions;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.RemoveComment;

/// <summary>
/// Removes (soft-deletes) a comment through the ticket aggregate. The framework's
/// <see cref="RemoveChildEntityHandlerBase{TCommand, TParent, TIdentifierType}"/> is the
/// load-mutate-save pipeline with the child collection made a required include, because a remove
/// that cannot see the collection cannot find the comment and reports a wrong <c>NotFound</c>.
/// </summary>
public sealed class RemoveCommentHandler(IUnitOfWork unitOfWork)
    : RemoveChildEntityHandlerBase<RemoveCommentCommand, Ticket, TicketIdentifierType>(unitOfWork)
{
    /// <inheritdoc />
    protected override IEnumerable<string> Includes => [nameof(Ticket.Comments)];

    /// <inheritdoc />
    protected override TicketIdentifierType EntityId(RemoveCommentCommand command) => command.TicketId;

    /// <inheritdoc />
    protected override Task<Result> MutateAsync(
        Ticket ticket,
        RemoveCommentCommand command,
        CancellationToken cancellationToken) =>
        Task.FromResult(ticket.RemoveComment(command.CommentId));
}
