using MMCA.Common.Application.Interfaces.Infrastructure;
using MMCA.Common.Application.UseCases;
using MMCA.Common.Shared.Abstractions;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.RemoveComment;

/// <summary>
/// Removes (soft-deletes) a comment through the ticket aggregate (loaded tracked with its comments).
/// </summary>
public sealed class RemoveCommentHandler(IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveCommentCommand, Result>
{
    public async Task<Result> HandleAsync(RemoveCommentCommand command, CancellationToken cancellationToken = default)
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
                Error.NotFound.WithSource(nameof(RemoveCommentHandler)).WithTarget(nameof(Ticket)));
        }

        var result = ticket.RemoveComment(command.CommentId);
        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
