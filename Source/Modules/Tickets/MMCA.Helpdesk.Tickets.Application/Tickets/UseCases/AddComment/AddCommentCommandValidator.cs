using FluentValidation;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.AddComment;

/// <summary>
/// FluentValidation rules for <see cref="AddCommentCommand"/>, applied by the pipeline's Validating
/// decorator before the transaction opens. The limits are the aggregate's own, read from
/// <see cref="TicketInvariants"/> rather than restated, so the two answers cannot drift apart.
/// The aggregate keeps its check: the invariant belongs to the domain, and this validator only makes
/// the refusal arrive before a transaction and a database round trip were spent on it.
/// </summary>
public sealed class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        // Report every broken rule rather than stopping at the first, so one ProblemDetails payload
        // carries them all.
        ClassLevelCascadeMode = CascadeMode.Continue;

        RuleFor(x => x.Body).NotEmpty().MaximumLength(TicketInvariants.CommentBodyMaxLength);
    }
}
