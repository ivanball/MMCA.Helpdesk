// Tickets module entity identifier type aliases.
// Both entities use database-generated integer IDs (the [IdValueGenerated] attribute on the
// domain entities). This file is linked into every project solution-wide via Directory.Build.props,
// so the aliases are visible everywhere. Always use the alias instead of the raw type.
global using TicketCommentIdentifierType = int;
global using TicketIdentifierType = int;
