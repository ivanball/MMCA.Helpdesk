using FluentValidation;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.RemoveComment;

/// <summary>
/// FluentValidation rules for <see cref="RemoveCommentCommand"/>, applied by the pipeline's Validating
/// decorator before the transaction opens. Both members are route-supplied identifiers, so the rules
/// only assert that each one arrived; the pairing itself is the aggregate's business to check, and it
/// still does. Refusing a default id here costs no transaction and no database round trip, and it is
/// what keeps EVERY handled command covered by the validation gate rather than exempted from it.
/// </summary>
public sealed class RemoveCommentCommandValidator : AbstractValidator<RemoveCommentCommand>
{
    public RemoveCommentCommandValidator()
    {
        // Report every broken rule rather than stopping at the first, so one ProblemDetails payload
        // carries them all.
        ClassLevelCascadeMode = CascadeMode.Continue;

        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.CommentId).NotEmpty();
    }
}
