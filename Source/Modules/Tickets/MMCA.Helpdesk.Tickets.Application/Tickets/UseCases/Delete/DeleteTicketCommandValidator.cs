using FluentValidation;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Delete;

/// <summary>
/// FluentValidation rules for <see cref="DeleteTicketCommand"/>, applied by the pipeline's Validating
/// decorator before the transaction opens. The whole payload is a route-supplied identifier, so the
/// only thing there is to check is that one arrived: refusing the default value costs no transaction
/// and no database round trip. Small as it is, it is what keeps EVERY handled command covered by the
/// validation gate, which is the property <c>CommandValidatorCoverageTests</c> enforces and the
/// reason this slice ships a validator instead of an allowlist entry.
/// </summary>
public sealed class DeleteTicketCommandValidator : AbstractValidator<DeleteTicketCommand>
{
    public DeleteTicketCommandValidator() => RuleFor(x => x.TicketId).NotEmpty();
}
