using MMCA.Common.Application.Interfaces;
using MMCA.Common.Application.Interfaces.Infrastructure;
using MMCA.Common.Application.UseCases;
using MMCA.Common.Shared.Abstractions;
using MMCA.Helpdesk.Tickets.Application.Tickets.DTOs;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets.IntegrationEvents;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Create;

/// <summary>
/// Opens a new ticket: maps the request through the domain factory, persists via the unit of work
/// (which stamps audit fields and dispatches domain events), then publishes the
/// <see cref="TicketOpenedIntegrationEvent"/> for cross-module/cross-service consumers. Wrapped by
/// the decorator pipeline (logging, caching, validating, transactional) once
/// <c>AddApplicationDecorators()</c> runs.
/// </summary>
public sealed class CreateTicketHandler(
    IUnitOfWork unitOfWork,
    IEntityRequestMapper<Ticket, TicketCreateRequest, TicketIdentifierType> requestMapper,
    IEventBus eventBus,
    TicketDTOMapper dtoMapper) : ICommandHandler<TicketCreateRequest, Result<TicketDTO>>
{
    public async Task<Result<TicketDTO>> HandleAsync(TicketCreateRequest command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = await requestMapper.CreateEntityAsync(command, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return Result.Failure<TicketDTO>(result.Errors);
        }

        var entity = result.Value!;
        var repository = unitOfWork.GetRepository<Ticket, TicketIdentifierType>();

        await repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Published after the commit so the database-generated ticket id is populated by the time the
        // event reaches consumers. The publisher persists the event to the outbox and dispatches it
        // in-process today, and will route it over a broker once Tickets is extracted, with no handler
        // code change required.
        await eventBus.PublishAsync(
            new TicketOpenedIntegrationEvent(entity.Id, entity.RequesterUserId),
            cancellationToken).ConfigureAwait(false);

        return Result.Success(dtoMapper.MapToDTO(entity));
    }
}
