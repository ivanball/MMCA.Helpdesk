using MMCA.Common.Application.Interfaces.Infrastructure;
using MMCA.Common.Application.UseCases;
using MMCA.Common.Shared.Abstractions;
using MMCA.Helpdesk.Tickets.Application.Tickets.DTOs;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Update;

/// <summary>
/// Updates a ticket's editable details through the aggregate root, then returns the refreshed DTO.
/// The load-mutate-save workflow (load tracked with the includes the mutation needs, fail with
/// <c>NotFound</c> when the ticket is gone, stamp the concurrency token, save only when the domain
/// method succeeded) is the framework's
/// <see cref="MutateEntityHandlerBase{TCommand, TEntity, TIdentifierType, TEntityDTO}"/>; the
/// overrides below are the only per-use-case parts.
/// </summary>
public sealed class UpdateTicketHandler(
    IUnitOfWork unitOfWork,
    TicketDTOMapper dtoMapper)
    : MutateEntityHandlerBase<UpdateTicketCommand, Ticket, TicketIdentifierType, TicketDTO>(
        unitOfWork, dtoMapper)
{
    // template:begin child
    /// <summary>
    /// The comments are eager-loaded because the DTO this handler answers with carries them: a
    /// no-include load would return a ticket whose comment list is empty.
    /// </summary>
    protected override IEnumerable<string> Includes => [nameof(Ticket.Comments)];

    // template:end child
    /// <inheritdoc />
    protected override TicketIdentifierType EntityId(UpdateTicketCommand command) => command.TicketId;

    /// <summary>
    /// ADR-035: the base stamps the client's last-seen rowversion back as the original, so a
    /// conflicting concurrent edit fails the save (DbUpdateExceptionHandler maps it to 409). Null
    /// skips the check.
    /// </summary>
    /// <param name="command">The command being handled.</param>
    /// <returns>The client's last-observed row version, or null to skip the check.</returns>
    protected override byte[]? RowVersion(UpdateTicketCommand command) => command.RowVersion;

    /// <inheritdoc />
    protected override Task<Result> MutateAsync(
        Ticket ticket,
        UpdateTicketCommand command,
        CancellationToken cancellationToken)
    {
        var result = ticket.UpdateDetails(command.Title, command.Description);

        return Task.FromResult(result);
    }
}
