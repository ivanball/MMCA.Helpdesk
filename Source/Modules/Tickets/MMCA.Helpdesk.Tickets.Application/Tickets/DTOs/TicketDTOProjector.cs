using MMCA.Common.Application.Interfaces.Mapping;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;
using Riok.Mapperly.Abstractions;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.DTOs;

/// <summary>
/// Mapperly-generated server-side projection from <see cref="Ticket"/> to <see cref="TicketDTO"/>.
/// Kept in its own file rather than beside <see cref="TicketDTOMapper"/>: that file carries the
/// scaffold's optional-axis marker regions, and a projection has no axis-specific lines of its own.
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
internal static partial class TicketDTOProjection
{
    /// <summary>Projects a ticket queryable to a DTO queryable, server-side.</summary>
    /// <param name="source">The entity queryable.</param>
    /// <returns>The projected DTO queryable.</returns>
    internal static partial IQueryable<TicketDTO> ProjectToDTO(IQueryable<Ticket> source);
}

/// <summary>
/// The <see cref="IEntityDTOProjector{TEntity, TEntityDTO, TIdentifierType}"/> wrapper around
/// <see cref="TicketDTOProjection"/>. It needs no hand-written registration: the module's
/// <c>ScanModuleApplicationServices&lt;ClassReference&gt;()</c> scan picks up
/// <c>IEntityDTOProjector</c> implementations beside the DTO mappers, which is what switches the
/// query service's list reads from materialize-then-map onto projection pushdown.
/// </summary>
public sealed class TicketDTOProjector
    : IEntityDTOProjector<Ticket, TicketDTO, TicketIdentifierType>
{
    /// <inheritdoc />
    public IQueryable<TicketDTO> ProjectTo(IQueryable<Ticket> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return TicketDTOProjection.ProjectToDTO(source);
    }
}
