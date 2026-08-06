using FluentValidation;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Create;

/// <summary>
/// FluentValidation rules for <see cref="TicketCreateRequest"/>, applied by the pipeline's
/// Validating decorator before the transaction opens.
/// </summary>
public sealed class TicketCreateRequestValidator : AbstractValidator<TicketCreateRequest>
{
    public TicketCreateRequestValidator()
    {
        // Report every broken rule rather than stopping at the first, so one ProblemDetails payload
        // carries them all. This is FluentValidation's default, stated rather than assumed: it also
        // keeps this constructor a block body whatever shape the scaffold generates (a validator
        // reduced to one rule would otherwise have to become an expression body, IDE0021).
        ClassLevelCascadeMode = CascadeMode.Continue;

        RuleFor(x => x.Title).NotEmpty().MaximumLength(TicketInvariants.TitleMaxLength);
        // template:begin description
        RuleFor(x => x.Description).NotEmpty().MaximumLength(TicketInvariants.DescriptionMaxLength);
        // template:end description
        // template:begin owner
        RuleFor(x => x.RequesterUserId).GreaterThan(0);
        // template:end owner
    }
}
