using MMCA.Common.Shared.Abstractions;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Tickets.Domain.Tickets;

/// <summary>
/// Business invariants for the <c>Ticket</c> aggregate. Each method returns a <see cref="Result"/>
/// so callers can compose them with <see cref="Result.Combine(System.ReadOnlySpan{Result})"/>.
/// </summary>
public static class TicketInvariants
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 4000;
    public const int CommentBodyMaxLength = 4000;

    public static Result EnsureTitleIsValid(string title, string source)
        => string.IsNullOrWhiteSpace(title) || title.Length > TitleMaxLength
            ? Result.Failure(Error.Invariant(
                code: "Ticket.Title.Invalid",
                message: $"Title is required and must be at most {TitleMaxLength} characters.",
                source: source,
                target: nameof(title)))
            : Result.Success();

    public static Result EnsureDescriptionIsValid(string description, string source)
        => string.IsNullOrWhiteSpace(description) || description.Length > DescriptionMaxLength
            ? Result.Failure(Error.Invariant(
                code: "Ticket.Description.Invalid",
                message: $"Description is required and must be at most {DescriptionMaxLength} characters.",
                source: source,
                target: nameof(description)))
            : Result.Success();

    public static Result EnsureCommentBodyIsValid(string body, string source)
        => string.IsNullOrWhiteSpace(body) || body.Length > CommentBodyMaxLength
            ? Result.Failure(Error.Invariant(
                code: "Ticket.Comment.Body.Invalid",
                message: $"Comment body is required and must be at most {CommentBodyMaxLength} characters.",
                source: source,
                target: nameof(body)))
            : Result.Success();

    public static Result EnsureStatusAllowsComments(TicketStatus status, string source)
        => status == TicketStatus.Closed
            ? Result.Failure(Error.Invariant(
                code: "Ticket.Closed",
                message: "Comments cannot be added to a closed ticket.",
                source: source,
                target: nameof(status)))
            : Result.Success();
}
