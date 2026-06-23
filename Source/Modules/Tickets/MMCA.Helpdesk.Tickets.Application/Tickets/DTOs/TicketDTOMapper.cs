using MMCA.Common.Application.Interfaces;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;
using Riok.Mapperly.Abstractions;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.DTOs;

/// <summary>
/// Maps the <see cref="Ticket"/> aggregate to <see cref="TicketDTO"/> (Mapperly), delegating child
/// comment mapping to <see cref="TicketCommentDTOMapper"/>.
/// </summary>
[Mapper]
public sealed partial class TicketDTOMapper(TicketCommentDTOMapper ticketCommentDTOMapper)
    : IEntityDTOMapper<Ticket, TicketDTO, TicketIdentifierType>
{
    [UseMapper]
    private readonly TicketCommentDTOMapper _ticketCommentDTOMapper = ticketCommentDTOMapper;

    public partial TicketDTO MapToDTO(Ticket entity);

    public IReadOnlyCollection<TicketDTO> MapToDTOs(IReadOnlyCollection<Ticket> entityCollection)
    {
        ArgumentNullException.ThrowIfNull(entityCollection);
        return [.. entityCollection.Select(MapToDTO)];
    }
}
