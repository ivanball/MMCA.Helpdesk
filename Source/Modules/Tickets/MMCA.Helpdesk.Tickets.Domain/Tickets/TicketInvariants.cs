using MMCA.Common.Domain.Invariants;
using MMCA.Common.Shared.Abstractions;
// template:begin childStatus
using MMCA.Helpdesk.Tickets.Shared.Tickets;

// template:end childStatus
namespace MMCA.Helpdesk.Tickets.Domain.Tickets;

/// <summary>
/// Business invariants for the <c>Ticket</c> aggregate. Each method returns a <see cref="Result"/>
/// so callers can compose them with <see cref="Result.Combine(System.ReadOnlySpan{Result})"/>.
/// The string checks delegate to the framework's <see cref="CommonInvariants"/> helpers, so each
/// field reports a distinct empty vs too-long error; only the app-specific rules and the
/// length constants live here.
/// </summary>
public static class TicketInvariants
{
    public const int TitleMaxLength = 200;
    // template:begin description
    public const int DescriptionMaxLength = 4000;
    // template:end description
    // template:begin child
    public const int CommentBodyMaxLength = 4000;

    // template:end child
    public static Result EnsureTitleIsValid(string title, string source)
        => Result.Combine(
            CommonInvariants.EnsureStringIsNotEmpty(title, "Ticket.Title.Empty", "Ticket title cannot be empty.", source, nameof(title)),
            CommonInvariants.EnsureStringMaxLength(title, TitleMaxLength, "Ticket.Title.TooLong", $"Ticket title cannot exceed {TitleMaxLength} characters.", source, nameof(title)));

    // template:begin description
    public static Result EnsureDescriptionIsValid(string description, string source)
        => Result.Combine(
            CommonInvariants.EnsureStringIsNotEmpty(description, "Ticket.Description.Empty", "Ticket description cannot be empty.", source, nameof(description)),
            CommonInvariants.EnsureStringMaxLength(description, DescriptionMaxLength, "Ticket.Description.TooLong", $"Ticket description cannot exceed {DescriptionMaxLength} characters.", source, nameof(description)));

    // template:end description
    // template:begin child
    public static Result EnsureCommentBodyIsValid(string body, string source)
        => Result.Combine(
            CommonInvariants.EnsureStringIsNotEmpty(body, "Ticket.Comment.Body.Empty", "Comment body cannot be empty.", source, nameof(body)),
            CommonInvariants.EnsureStringMaxLength(body, CommentBodyMaxLength, "Ticket.Comment.Body.TooLong", $"Comment body cannot exceed {CommentBodyMaxLength} characters.", source, nameof(body)));

    // template:end child
    // template:begin childStatus
    public static Result EnsureStatusAllowsComments(TicketStatus status, string source)
        => status == TicketStatus.Closed
            ? Result.Failure(Error.Invariant(
                code: "Ticket.Closed",
                message: "Comments cannot be added to a closed ticket.",
                source: source,
                target: nameof(status)))
            : Result.Success();
    // template:end childStatus
}
