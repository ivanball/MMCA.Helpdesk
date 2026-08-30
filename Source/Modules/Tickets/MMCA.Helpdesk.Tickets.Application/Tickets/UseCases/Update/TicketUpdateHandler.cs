using MMCA.Common.Application.Interfaces;
using MMCA.Common.Application.Interfaces.Infrastructure;
using MMCA.Common.Application.UseCases;
using MMCA.Helpdesk.Tickets.Application.Tickets.DTOs;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Update;

/// <summary>
/// The module's update handler: the framework's generic
/// <see cref="UpdateEntityHandler{TEntity, TEntityDTO, TIdentifierType, TUpdateRequest}"/> with the
/// one thing a ticket update needs beyond the shared workflow, an eager load of the comments.
/// </summary>
/// <remarks>
/// <para>
/// The generic handler is left unsealed by the framework for exactly this: the load-mutate-save
/// workflow, the optimistic-concurrency stamp (ADR-035) and the refreshed DTO all stay shared, while
/// naming the navigation an aggregate's response carries stays a per-aggregate decision. The DTO this
/// answers with lists the ticket's comments, so a no-include load would return a ticket whose comment
/// list is empty on every successful edit.
/// </para>
/// <para>
/// It is picked up by <c>ScanModuleApplicationServices</c> like any other handler, and
/// <c>AddEntityCrud</c> runs after that scan with <c>TryAdd</c>, so this subclass keeps the update
/// verb rather than being displaced by the plain generic handler. An aggregate with no child
/// collection needs no subclass at all: <c>AddEntityCrud</c> alone is the whole update slice.
/// </para>
/// </remarks>
/// <param name="unitOfWork">The ambient unit of work.</param>
/// <param name="updateApplier">The module's applier, which calls the aggregate's guarded method.</param>
/// <param name="dtoMapper">The module's entity-to-DTO mapper.</param>
public sealed class TicketUpdateHandler(
    IUnitOfWork unitOfWork,
    IEntityUpdateApplier<Ticket, TicketUpdateRequest, TicketIdentifierType> updateApplier,
    TicketDTOMapper dtoMapper)
    : UpdateEntityHandler<Ticket, TicketDTO, TicketIdentifierType, TicketUpdateRequest>(
        unitOfWork, updateApplier, dtoMapper)
{
    /// <inheritdoc />
    protected override IEnumerable<string> Includes => [nameof(Ticket.Comments)];
}
