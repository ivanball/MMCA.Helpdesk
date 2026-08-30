using MMCA.Common.Application.Interfaces;
using MMCA.Common.Shared.Abstractions;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Update;

/// <summary>
/// Applies a <see cref="TicketUpdateRequest"/> to a loaded <see cref="Ticket"/> by delegating to the
/// aggregate's guarded <see cref="Ticket.UpdateDetails"/> method, so the framework's generic
/// <c>UpdateEntityHandler</c> never has to know a ticket field name.
/// </summary>
/// <remarks>
/// This is the write-side twin of <see cref="Create.TicketCreateRequestMapper"/>: the mapper owns
/// "request to a NEW aggregate", the applier owns "request onto an EXISTING one". Both are picked up
/// by <c>ScanModuleApplicationServices</c>, so the module writes the class and registers nothing.
/// The invariants and the <c>TicketChanged</c> domain event stay inside the aggregate method; a
/// property assignment here would bypass both.
/// </remarks>
public sealed class TicketUpdateApplier
    : IEntityUpdateApplier<Ticket, TicketUpdateRequest, TicketIdentifierType>
{
    /// <inheritdoc />
    public Task<Result> ApplyAsync(
        Ticket entity,
        TicketUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(entity.UpdateDetails(request.Title, request.Description));
    }
}
