using MMCA.Common.Application.Interfaces.Infrastructure;
using MMCA.Common.Application.UseCases;
using MMCA.Common.Shared.Abstractions;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Delete;

/// <summary>
/// Soft-deletes a ticket through the aggregate root (loaded tracked with its comments so the cascade
/// soft-deletes them too). The EF global query filter then excludes it from subsequent reads.
/// </summary>
public sealed class DeleteTicketHandler(IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteTicketCommand, Result>
{
    public async Task<Result> HandleAsync(DeleteTicketCommand command, CancellationToken cancellationToken = default)
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
                Error.NotFound.WithSource(nameof(DeleteTicketHandler)).WithTarget(nameof(Ticket)));
        }

        var result = ticket.Delete();
        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
