using MMCA.Common.Application.Interfaces.Infrastructure.Persistence;
using MMCA.Common.Application.UseCases.Crud;
using MMCA.Common.Shared.Abstractions;
using MMCA.Helpdesk.Tickets.Application.Tickets.DTOs;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.AddComment;

/// <summary>
/// Appends a comment to a ticket. The canonical eager-load-then-mutate idiom for adding a child to
/// an aggregate is the framework's
/// <see cref="AddChildEntityHandlerBase{TCommand, TParent, TIdentifierType, TChild, TChildDTO}"/>:
/// load the ticket tracked and with its comments, fail with <c>NotFound</c> when it is gone,
/// delegate to the aggregate method that owns the invariant, save only on success, and answer with
/// the new comment's DTO.
/// </summary>
public sealed class AddCommentHandler(
    IUnitOfWork unitOfWork,
    TicketCommentDTOMapper commentDTOMapper)
    : AddChildEntityHandlerBase<AddCommentCommand, Ticket, TicketIdentifierType, TicketComment, TicketCommentDTO>(
        unitOfWork)
{
    /// <summary>
    /// The comments are eager-loaded because the aggregate's add rule reads the existing collection:
    /// naming the navigation is deliberate rather than inherited, which is why the base leaves it
    /// abstract.
    /// </summary>
    protected override IEnumerable<string> Includes => [nameof(Ticket.Comments)];

    /// <inheritdoc />
    protected override TicketIdentifierType ParentId(AddCommentCommand command) => command.TicketId;

    /// <inheritdoc />
    protected override Result<TicketComment> Apply(Ticket parent, AddCommentCommand command)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(command);

        return parent.AddComment(id: null, command.Body, command.AuthorUserId);
    }

    /// <inheritdoc />
    protected override TicketCommentDTO MapChild(TicketComment child) => commentDTOMapper.MapToDTO(child);
}
