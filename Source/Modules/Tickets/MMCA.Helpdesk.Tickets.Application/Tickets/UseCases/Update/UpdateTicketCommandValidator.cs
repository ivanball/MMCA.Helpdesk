using FluentValidation;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Update;

/// <summary>
/// FluentValidation rules for <see cref="UpdateTicketCommand"/>, applied by the pipeline's Validating
/// decorator before the transaction opens. They mirror
/// <see cref="Create.TicketCreateRequestValidator"/> field for field, because an update that accepted
/// a title a create would have refused would let an aggregate reach a state it could never have been
/// created in.
/// </summary>
public sealed class UpdateTicketCommandValidator : AbstractValidator<UpdateTicketCommand>
{
    public UpdateTicketCommandValidator()
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
