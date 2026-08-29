using MMCA.Common.Application.Interfaces.Infrastructure;
using MMCA.Common.Application.UseCases;
using MMCA.Common.Shared.Abstractions;
using MMCA.Helpdesk.Tickets.Application.Tickets.DTOs;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.GetById;

/// <summary>
/// Loads a single ticket with its children (the list endpoint omits them) and maps it to a DTO.
/// </summary>
public sealed class GetTicketByIdHandler(IUnitOfWork unitOfWork, TicketDTOMapper dtoMapper)
    : IQueryHandler<GetTicketByIdQuery, Result<TicketDTO>>
{
    public async Task<Result<TicketDTO>> HandleAsync(GetTicketByIdQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // A query handler asks the unit of work for the READ repository: GetRepository would hand back
        // the write surface (Add/Update/SaveChanges) this read has no business holding. The local is
        // typed to the narrow IEntityReader rather than the wide IReadRepository because a by-id
        // lookup is all this handler does, so the declaration states the access it actually needs.
        IEntityReader<Ticket, TicketIdentifierType> repository =
            unitOfWork.GetReadRepository<Ticket, TicketIdentifierType>();
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
