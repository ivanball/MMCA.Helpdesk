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
        RuleFor(x => x.Title).NotEmpty().MaximumLength(TicketInvariants.TitleMaxLength);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(TicketInvariants.DescriptionMaxLength);
        RuleFor(x => x.RequesterUserId).GreaterThan(0);
    }
}
