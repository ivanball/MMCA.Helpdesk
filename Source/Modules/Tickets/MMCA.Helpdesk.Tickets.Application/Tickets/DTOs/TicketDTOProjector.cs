using MMCA.Common.Application.Interfaces.Mapping;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;
using Riok.Mapperly.Abstractions;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.DTOs;

/// <summary>
/// Mapperly-generated server-side projection from <see cref="Ticket"/> to <see cref="TicketDTO"/>,
/// and the module's <see cref="IEntityDTOProjector{TEntity, TEntityDTO, TIdentifierType}"/> in one
/// type: the class is itself the Mapperly mapper, and <see cref="ProjectTo"/> is the generated
/// projection. It needs no hand-written registration: the module's
/// <c>ScanModuleApplicationServices&lt;ClassReference&gt;()</c> scan picks up
/// <c>IEntityDTOProjector</c> implementations beside the DTO mappers, which is what switches the
/// query service's list reads from materialize-then-map onto projection pushdown. Kept in its own
/// file rather than beside <see cref="TicketDTOMapper"/>, so each Mapperly class declares exactly
/// one mapping entry point.
/// </summary>
/// <remarks>
/// A projection is an expression tree the database provider has to translate, so it cannot call an
/// instance sub-mapper: there is no <c>[UseMapper]</c> here.
/// template:begin child
/// The children are projected inline by Mapperly's nested projection instead, while the instance
/// mapper keeps delegating to <see cref="TicketCommentDTOMapper"/>.
/// template:end child
/// A test pins the projected values to the instance mapper's.
/// </remarks>
[Mapper]
public sealed partial class TicketDTOProjector
    : IEntityDTOProjector<Ticket, TicketDTO, TicketIdentifierType>
{
    /// <inheritdoc />
    public partial IQueryable<TicketDTO> ProjectTo(IQueryable<Ticket> source);

    // template:begin status
    // The lifecycle member becomes the wire enum. Mapperly picks this user mapping up by its
    // signature and inlines its expression body into the projection, and the provider evaluates it on
    // the client in the final projection, after reading the status column through its converter.
    // Internal rather than private: once inlined, nothing calls the method itself, and an unused
    // private member is an analyzer error.
    internal static TicketStatus MapStatus(TicketLifecycle status) => status.ToContract();
    // template:end status
}
