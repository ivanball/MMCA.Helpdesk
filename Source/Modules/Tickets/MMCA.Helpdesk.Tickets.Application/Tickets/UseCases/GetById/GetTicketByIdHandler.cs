using MMCA.Common.Application.Interfaces.Infrastructure;
using MMCA.Common.Application.UseCases;
using MMCA.Common.Shared.Abstractions;
using MMCA.Helpdesk.Tickets.Application.Tickets.DTOs;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.GetById;

/// <summary>
/// Loads a single ticket with its comments (the list endpoint omits children) and maps it to a DTO.
/// </summary>
public sealed class GetTicketByIdHandler(IUnitOfWork unitOfWork, TicketDTOMapper dtoMapper)
    : IQueryHandler<GetTicketByIdQuery, Result<TicketDTO>>
{
    public async Task<Result<TicketDTO>> HandleAsync(GetTicketByIdQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var repository = unitOfWork.GetRepository<Ticket, TicketIdentifierType>();
        var ticket = await repository.GetByIdAsync(
            query.Id,
            includes: [nameof(Ticket.Comments)],
            asTracking: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ticket is null
            ? Result.Failure<TicketDTO>(
                Error.NotFound.WithSource(nameof(GetTicketByIdHandler)).WithTarget(nameof(Ticket)))
            : Result.Success(dtoMapper.MapToDTO(ticket));
    }
}
