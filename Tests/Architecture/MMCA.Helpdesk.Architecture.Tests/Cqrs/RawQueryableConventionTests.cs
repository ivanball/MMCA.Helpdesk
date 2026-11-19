namespace MMCA.Helpdesk.Architecture.Tests.Cqrs;

/// <summary>
/// Raw-queryable convention (ADR-055), driven by the shared <see cref="RawQueryableConventionTestsBase"/>:
/// Helpdesk's module Application code never touches the repository's raw <c>IQueryable</c> surfaces
/// (<c>Table</c>, <c>TableNoTracking*</c>), because a handler written against one is EF-coupled and
/// cannot move behind a gRPC boundary when its module is extracted into its own service.
/// <para>
/// <see cref="RawQueryableConventionTestsBase.AllowedFiles"/> is deliberately not overridden: the
/// allowlist is an adoption ratchet for a repo with existing raw-queryable handlers, and an entry
/// written here would name this module's files and cover nothing in a module an adopter scaffolds.
/// Use the focused repository methods instead.
/// </para>
/// </summary>
public sealed class RawQueryableConventionTests : RawQueryableConventionTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}
