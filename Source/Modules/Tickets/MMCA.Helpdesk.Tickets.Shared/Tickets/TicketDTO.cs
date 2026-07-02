using MMCA.Common.Shared.DTOs;

namespace MMCA.Helpdesk.Tickets.Shared.Tickets;

/// <summary>
/// Read model for a <c>Ticket</c> aggregate returned by the API. Exposes the current
/// <see cref="RowVersion"/> so a client can echo it back on <c>TicketUpdateRequest</c> (ADR-035).
/// </summary>
public record class TicketDTO : IBaseDTO<TicketIdentifierType>, IConcurrencyAware
{
    public required TicketIdentifierType Id { get; init; }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "byte[] round-trips the EF rowversion concurrency token")]
    public byte[]? RowVersion { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required TicketStatus Status { get; init; }
    public required int RequesterUserId { get; init; }
    public IReadOnlyCollection<TicketCommentDTO> Comments { get; init; } = [];
}
