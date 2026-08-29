using FluentValidation;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.ChangeStatus;

/// <summary>
/// FluentValidation rules for <see cref="ChangeTicketStatusCommand"/>, applied by the pipeline's
/// Validating decorator before the transaction opens.
/// </summary>
public sealed class ChangeTicketStatusCommandValidator : AbstractValidator<ChangeTicketStatusCommand>
{
    public ChangeTicketStatusCommandValidator()
    {
        // Report every broken rule rather than stopping at the first, so one ProblemDetails payload
        // carries them all.
        ClassLevelCascadeMode = CascadeMode.Continue;

        // The property is typed, but the type is not the guarantee it looks like: an enum is a
        // number, so a request body carrying 7 deserializes into a TicketStatus no member names, and
        // ChangeStatus would store it. IsInEnum is what makes the declared type actually hold.
        RuleFor(x => x.Status).IsInEnum();
    }
}
