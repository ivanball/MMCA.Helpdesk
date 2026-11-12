// Tickets module entity identifier type aliases.
// The entities use database-generated integer IDs (the [IdValueGenerated] attribute on the
// domain entities). This file is linked into every project solution-wide via Directory.Build.props,
// so the aliases are visible everywhere. Always use the alias instead of the raw type.
// template:begin child
// The child id is a strongly typed wrapper over the same int (ADR-115); the ticket id stays the
// primitive alias, so the module shows both styles side by side.
global using TicketCommentIdentifierType = MMCA.Helpdesk.Tickets.Shared.Tickets.TicketCommentId;
// template:end child
global using TicketIdentifierType = int;
