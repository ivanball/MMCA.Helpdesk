using MMCA.Common.Application.Interfaces.Infrastructure;
using MMCA.Common.Application.UseCases;
using MMCA.Common.Shared.Abstractions;
using MMCA.Helpdesk.Tickets.Application.Tickets.DTOs;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.AddComment;

/// <summary>
/// Loads the ticket (tracked, with its comments), appends a comment through the aggregate root, and
/// saves. Demonstrates the canonical eager-load-then-mutate idiom for adding a child to an aggregate.
/// </summary>
public sealed class AddCommentHandler(
    IUnitOfWork unitOfWork,
    TicketCommentDTOMapper commentDTOMapper) : ICommandHandler<AddCommentCommand, Result<TicketCommentDTO>>
{
    public async Task<Result<TicketCommentDTO>> HandleAsync(AddCommentCommand command, CancellationToken cancellationToken = default)
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
            return Result.Failure<TicketCommentDTO>(
                Error.NotFound.WithSource(nameof(AddCommentHandler)).WithTarget(nameof(Ticket)));
        }

        var result = ticket.AddComment(id: null, command.Body, command.AuthorUserId);
        if (result.IsFailure)
        {
            return Result.Failure<TicketCommentDTO>(result.Errors);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(commentDTOMapper.MapToDTO(result.Value!));
    }
}
