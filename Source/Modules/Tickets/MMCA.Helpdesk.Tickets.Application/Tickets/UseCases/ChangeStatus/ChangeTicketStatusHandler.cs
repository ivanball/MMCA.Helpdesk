using MMCA.Common.Application.Interfaces.Infrastructure;
using MMCA.Common.Application.UseCases;
using MMCA.Common.Shared.Abstractions;
using MMCA.Helpdesk.Tickets.Application.Tickets.DTOs;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.ChangeStatus;

/// <summary>
/// Changes a ticket's status through the aggregate root, then returns the refreshed DTO.
/// </summary>
public sealed class ChangeTicketStatusHandler(IUnitOfWork unitOfWork, TicketDTOMapper dtoMapper)
    : ICommandHandler<ChangeTicketStatusCommand, Result<TicketDTO>>
{
    public async Task<Result<TicketDTO>> HandleAsync(ChangeTicketStatusCommand command, CancellationToken cancellationToken = default)
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
            return Result.Failure<TicketDTO>(
                Error.NotFound.WithSource(nameof(ChangeTicketStatusHandler)).WithTarget(nameof(Ticket)));
        }

        var result = ticket.ChangeStatus(command.Status);
        if (result.IsFailure)
        {
            return Result.Failure<TicketDTO>(result.Errors);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(dtoMapper.MapToDTO(ticket));
    }
}
