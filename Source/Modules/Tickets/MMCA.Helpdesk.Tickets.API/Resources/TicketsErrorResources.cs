namespace MMCA.Helpdesk.Tickets.API.Resources;

/// <summary>
/// Resource anchor type for the Tickets module's error-code translations (ADR-027). Its <c>.resx</c>
/// siblings (<c>TicketsErrorResources.resx</c> / <c>TicketsErrorResources.es.resx</c>) are keyed by
/// domain error <c>Code</c> (e.g. <c>"Ticket.Closed"</c>, see <c>TicketInvariants</c>) and resolved by
/// the shared <c>IErrorLocalizer</c> after the host registers this type via
/// <c>AddErrorResources&lt;TicketsErrorResources&gt;()</c>. The length limits are baked into the values
/// because they are compile-time constants (<c>TicketInvariants.TitleMaxLength</c> etc.); an unmapped
/// code degrades gracefully to its English message.
/// </summary>
public sealed class TicketsErrorResources;
