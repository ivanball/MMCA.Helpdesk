using MMCA.Common.Application.Interfaces.Infrastructure;
using MMCA.Common.Application.UseCases;
using MMCA.Common.Shared.Abstractions;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.EditComment;

/// <summary>
/// Edits a comment's body through the ticket aggregate (loaded tracked with its comments).
/// </summary>
public sealed class EditCommentHandler(IUnitOfWork unitOfWork)
    : ICommandHandler<EditCommentCommand, Result>
{
    public async Task<Result> HandleAsync(EditCommentCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var repository = unitOfWork.GetRepository<Ticket, TicketIdentifierType>();
        var ticket = await repository.GetByIdAsync(
            command.TicketId,
            includes: [nameof(Ticket.Comments)],
            asTracking: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (ticket is null)
        {
            return Result.Failure(
                Error.NotFound.WithSource(nameof(EditCommentHandler)).WithTarget(nameof(Ticket)));
        }

        var result = ticket.EditComment(command.CommentId, command.Body);
        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
