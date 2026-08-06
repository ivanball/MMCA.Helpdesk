using MMCA.Common.Application.Interfaces;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;
using Riok.Mapperly.Abstractions;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.DTOs;

/// <summary>
/// Maps the <see cref="Ticket"/> aggregate to <see cref="TicketDTO"/> (Mapperly).
/// template:begin child
/// Child comment mapping is delegated to <see cref="TicketCommentDTOMapper"/>.
/// template:end child
/// </summary>
[Mapper]
public sealed partial class TicketDTOMapper
    : IEntityDTOMapper<Ticket, TicketDTO, TicketIdentifierType>
{
    // template:begin child
    // A field plus an explicit constructor rather than a primary-constructor parameter: the child
    // axis is optional in the scaffold, and a whole-line region can drop a field and a constructor
    // where it could not drop one parameter out of a declaration line.
    [UseMapper]
    private readonly TicketCommentDTOMapper _ticketCommentDTOMapper;

    public TicketDTOMapper(TicketCommentDTOMapper ticketCommentDTOMapper)
        => _ticketCommentDTOMapper = ticketCommentDTOMapper;

    // template:end child
    public partial TicketDTO MapToDTO(Ticket entity);

    public IReadOnlyCollection<TicketDTO> MapToDTOs(IReadOnlyCollection<Ticket> entityCollection)
    {
        ArgumentNullException.ThrowIfNull(entityCollection);
        return [.. entityCollection.Select(MapToDTO)];
    }
}
