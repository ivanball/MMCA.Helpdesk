using MMCA.Common.Shared.DTOs;

namespace MMCA.Helpdesk.Tickets.Shared.Tickets;

/// <summary>
/// Read model for a <c>Ticket</c> aggregate returned by the API.
/// </summary>
public record class TicketDTO : IBaseDTO<TicketIdentifierType>
{
    public required TicketIdentifierType Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required TicketStatus Status { get; init; }
    public required int RequesterUserId { get; init; }
    public IReadOnlyCollection<TicketCommentDTO> Comments { get; init; } = [];
}
