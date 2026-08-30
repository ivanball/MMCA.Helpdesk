using MMCA.Common.Application.Interfaces.Infrastructure;
using MMCA.Common.Application.UseCases;
using MMCA.Common.Shared.Abstractions;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.EditComment;

/// <summary>
/// Edits a comment's body through the ticket aggregate. The load-mutate-save workflow is the
/// framework's <see cref="MutateEntityHandlerBase{TCommand, TEntity, TIdentifierType}"/>, whose bare
/// <see cref="Result"/> shape is the right one here: the caller needs success or the refused
/// invariant, not a re-rendered ticket.
/// </summary>
public sealed class EditCommentHandler(IUnitOfWork unitOfWork)
    : MutateEntityHandlerBase<EditCommentCommand, Ticket, TicketIdentifierType>(unitOfWork)
{
    /// <summary>
    /// The comments are eager-loaded because the aggregate searches that collection for the comment
    /// being edited: without them the edit would report a wrong <c>NotFound</c>.
    /// </summary>
    protected override IEnumerable<string> Includes => [nameof(Ticket.Comments)];

    /// <inheritdoc />
    protected override TicketIdentifierType EntityId(EditCommentCommand command) => command.TicketId;

    /// <summary>
    /// ADR-035: the concurrency token belongs to the aggregate ROOT, so a comment edit conditioned on
    /// a stale ticket version fails the save, which <c>[SupportsIfMatch]</c> answers as a 412.
    /// </summary>
    /// <param name="command">The command being handled.</param>
    /// <returns>The client's last-observed row version.</returns>
    protected override byte[]? RowVersion(EditCommentCommand command) => command.RowVersion;

    /// <inheritdoc />
    protected override Task<Result> MutateAsync(
        Ticket ticket,
        EditCommentCommand command,
        CancellationToken cancellationToken) =>
        Task.FromResult(ticket.EditComment(command.CommentId, command.Body));
}
