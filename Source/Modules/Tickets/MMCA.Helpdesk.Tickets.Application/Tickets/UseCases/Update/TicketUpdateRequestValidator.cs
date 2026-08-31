using FluentValidation;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Update;

/// <summary>
/// FluentValidation rules for <see cref="TicketUpdateRequest"/>, applied by the pipeline's Validating
/// decorator before the transaction opens. They mirror
/// <see cref="Create.TicketCreateRequestValidator"/> field for field, because an update that accepted
/// a title a create would have refused would let an aggregate reach a state it could never have been
/// created in.
/// </summary>
/// <remarks>
/// The rules are written against the REQUEST rather than against a command, which is what lets the
/// framework's generic <c>UpdateEntityCommand</c> carry them: <c>AddEntityCrud</c> registers the
/// <c>CommandRequestValidator</c> bridge that unwraps the command and runs this validator on its
/// payload.
/// </remarks>
public sealed class TicketUpdateRequestValidator : AbstractValidator<TicketUpdateRequest>
{
    public TicketUpdateRequestValidator()
    {
        // Report every broken rule rather than stopping at the first, so one ProblemDetails payload
        // carries them all.
        ClassLevelCascadeMode = CascadeMode.Continue;

        RuleFor(x => x.Title).NotEmpty().MaximumLength(TicketInvariants.TitleMaxLength);
        // template:begin description
        RuleFor(x => x.Description).NotEmpty().MaximumLength(TicketInvariants.DescriptionMaxLength);
        // template:end description
    }
}
