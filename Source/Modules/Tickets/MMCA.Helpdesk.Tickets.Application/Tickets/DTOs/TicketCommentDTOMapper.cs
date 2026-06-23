using MMCA.Common.Application.Interfaces;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;
using Riok.Mapperly.Abstractions;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.DTOs;

/// <summary>
/// Maps <see cref="TicketComment"/> child entities to <see cref="TicketCommentDTO"/> (Mapperly).
/// </summary>
[Mapper]
public sealed partial class TicketCommentDTOMapper
    : IEntityDTOMapper<TicketComment, TicketCommentDTO, TicketCommentIdentifierType>
{
    public partial TicketCommentDTO MapToDTO(TicketComment entity);

    public IReadOnlyCollection<TicketCommentDTO> MapToDTOs(IReadOnlyCollection<TicketComment> entityCollection)
    {
        ArgumentNullException.ThrowIfNull(entityCollection);
        return [.. entityCollection.Select(MapToDTO)];
    }
}
