using MMCA.Common.Shared.DTOs;

namespace MMCA.Helpdesk.Tickets.Shared.Tickets;

/// <summary>
/// Read model for a <c>Ticket</c> aggregate returned by the API. Exposes the current
/// <see cref="RowVersion"/> so a client can state it as the <c>If-Match</c> precondition of its next
/// write (ADR-035).
/// </summary>
public record class TicketDTO : IBaseDTO<TicketIdentifierType>, IConcurrencyAware
{
    public required TicketIdentifierType Id { get; init; }

    /// <inheritdoc />
    public required byte[] RowVersion { get; init; }
    public required string Title { get; init; }
    // template:begin description
    public required string Description { get; init; }
    // template:end description
    // template:begin status
    public required TicketStatus Status { get; init; }
    // template:end status
    // template:begin owner
    public required int RequesterUserId { get; init; }
    // template:end owner
    // template:begin child
    public IReadOnlyCollection<TicketCommentDTO> Comments { get; init; } = [];
    // template:end child
}
