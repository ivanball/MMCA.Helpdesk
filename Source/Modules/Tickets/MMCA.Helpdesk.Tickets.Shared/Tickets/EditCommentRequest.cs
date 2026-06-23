namespace MMCA.Helpdesk.Tickets.Shared.Tickets;

/// <summary>
/// Request body for editing a comment's body (the ticket id and comment id come from the route).
/// </summary>
/// <param name="Body">The new comment text.</param>
public sealed record EditCommentRequest(string Body);
