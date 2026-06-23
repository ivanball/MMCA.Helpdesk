using MMCA.Common.Application.Interfaces;
using MMCA.Common.Shared.Abstractions;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Create;

/// <summary>
/// Maps a <see cref="TicketCreateRequest"/> to a new <see cref="Ticket"/> via the domain factory.
/// </summary>
public sealed class TicketCreateRequestMapper
    : IEntityRequestMapper<Ticket, TicketCreateRequest, TicketIdentifierType>
{
    public Task<Result<Ticket>> CreateEntityAsync(TicketCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(Ticket.Open(
            id: null,
            title: request.Title,
            description: request.Description,
            requesterUserId: request.RequesterUserId));
    }
}
