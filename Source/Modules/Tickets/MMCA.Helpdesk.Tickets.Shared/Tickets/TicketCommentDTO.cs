using MMCA.Common.Shared.DTOs;

namespace MMCA.Helpdesk.Tickets.Shared.Tickets;

/// <summary>
/// Read model for a <c>TicketComment</c> child entity.
/// </summary>
public record class TicketCommentDTO : IBaseDTO<TicketCommentIdentifierType>
{
    public required TicketCommentIdentifierType Id { get; init; }
    public required string Body { get; init; }
    public required int AuthorUserId { get; init; }
}
