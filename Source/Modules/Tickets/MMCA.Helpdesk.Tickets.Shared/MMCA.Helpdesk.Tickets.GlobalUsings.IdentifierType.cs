// Tickets module entity identifier type aliases.
// The entities use database-generated integer IDs (the [IdValueGenerated] attribute on the
// domain entities). This file is linked into every project solution-wide via Directory.Build.props,
// so the aliases are visible everywhere. Always use the alias instead of the raw type.
// template:begin child
global using TicketCommentIdentifierType = int;
// template:end child
global using TicketIdentifierType = int;
