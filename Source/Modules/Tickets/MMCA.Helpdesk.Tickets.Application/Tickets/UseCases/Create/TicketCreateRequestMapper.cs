using MMCA.Common.Application.Interfaces.Mapping;
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

        // Kept on ONE line deliberately: some of these arguments are optional axes of the scaffold,
        // and a comma-separated argument list cannot lose a middle or last element to a whole-line
        // conditional. The reference repo's staging script rewrites this line per generated shape.
        return Task.FromResult(Ticket.Create(id: null, title: request.Title, description: request.Description, requesterUserId: request.RequesterUserId));
    }
}
