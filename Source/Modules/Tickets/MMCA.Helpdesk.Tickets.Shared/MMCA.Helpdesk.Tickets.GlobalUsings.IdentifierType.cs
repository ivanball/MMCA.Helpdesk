// Tickets module entity identifier type aliases.
// The entities use database-generated integer IDs (the [IdValueGenerated] attribute on the
// domain entities). This file is linked into every project solution-wide via Directory.Build.props,
// so an alias here may only name a type every project can see: a primitive. Always use the alias
// instead of the raw primitive.
// A strongly typed identifier (ADR-115) gets NO alias: it is a type of this module, which another
// module's projects cannot reference, so code names the wrapper type directly.
global using TicketIdentifierType = int;
