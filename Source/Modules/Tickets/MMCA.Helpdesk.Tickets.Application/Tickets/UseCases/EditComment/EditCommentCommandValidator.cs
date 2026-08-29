using FluentValidation;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.EditComment;

/// <summary>
/// FluentValidation rules for <see cref="EditCommentCommand"/>, applied by the pipeline's Validating
/// decorator before the transaction opens. The body rules match the ones an added comment answers to
/// (<see cref="TicketInvariants"/>), because an edit that could write a body a create would have
/// refused is the same gap by another route. The identifiers carry no rule: a wrong id is a
/// <c>NotFound</c> the handler already answers, not a validation failure.
/// </summary>
public sealed class EditCommentCommandValidator : AbstractValidator<EditCommentCommand>
{
    public EditCommentCommandValidator()
    {
        // Report every broken rule rather than stopping at the first, so one ProblemDetails payload
        // carries them all.
        ClassLevelCascadeMode = CascadeMode.Continue;

        RuleFor(x => x.Body).NotEmpty().MaximumLength(TicketInvariants.CommentBodyMaxLength);
    }
}
