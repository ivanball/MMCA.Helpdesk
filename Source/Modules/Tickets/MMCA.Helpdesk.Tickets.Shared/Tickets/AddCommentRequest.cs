namespace MMCA.Helpdesk.Tickets.Shared.Tickets;

/// <summary>
/// Request body for appending a comment to a ticket (the ticket id comes from the route).
/// </summary>
/// <param name="Body">The comment text.</param>
/// <param name="AuthorUserId">The user id of the comment author.</param>
public sealed record AddCommentRequest(string Body, int AuthorUserId);
