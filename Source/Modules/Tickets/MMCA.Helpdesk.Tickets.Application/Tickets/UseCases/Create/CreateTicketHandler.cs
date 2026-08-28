using MMCA.Common.Application.Interfaces;
using MMCA.Common.Application.Interfaces.Infrastructure;
using MMCA.Common.Application.UseCases;
using MMCA.Helpdesk.Tickets.Application.Tickets.DTOs;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets.IntegrationEvents;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Create;

/// <summary>
/// Opens a new ticket. The create workflow itself (map the request through the domain factory,
/// persist via the unit of work, which stamps audit fields and dispatches domain events, then map
/// the refreshed DTO) is the framework's
/// <see cref="CreateEntityHandlerBase{TCreateRequest, TEntity, TIdentifierType, TEntityDTO}"/>, so
/// this handler carries only what is genuinely its own: the
/// <see cref="TicketOpenedIntegrationEvent"/> published for cross-module/cross-service consumers.
/// Wrapped by the decorator pipeline (logging, caching, validating, transactional) once
/// <c>AddApplicationDecorators()</c> runs.
/// </summary>
public sealed class CreateTicketHandler(
    IUnitOfWork unitOfWork,
    IEntityRequestMapper<Ticket, TicketCreateRequest, TicketIdentifierType> requestMapper,
    IEventBus eventBus,
    TicketDTOMapper dtoMapper)
    : CreateEntityHandlerBase<TicketCreateRequest, Ticket, TicketIdentifierType, TicketDTO>(
        unitOfWork, requestMapper, dtoMapper)
{
    /// <summary>
    /// Runs after the commit, so the database-generated ticket id is populated by the time the event
    /// reaches consumers. The publisher persists the event to the outbox and dispatches it in-process
    /// today, and will route it over a broker once Tickets is extracted, with no handler code change
    /// required.
    /// </summary>
    /// <param name="entity">The persisted ticket.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override async Task OnCreatedAsync(Ticket entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        await eventBus.PublishAsync(
            new TicketOpenedIntegrationEvent(entity.Id, entity.RequesterUserId),
            cancellationToken).ConfigureAwait(false);
    }
}
