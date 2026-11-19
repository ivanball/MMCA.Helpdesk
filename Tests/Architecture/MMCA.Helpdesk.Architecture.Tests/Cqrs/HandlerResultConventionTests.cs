namespace MMCA.Helpdesk.Architecture.Tests.Cqrs;

/// <summary>
/// Handler result convention (ADR-013), driven by the shared <see cref="HandlerResultConventionTestsBase"/>:
/// every command and query handler in Helpdesk's module Application assemblies returns
/// <c>Result</c> or <c>Result&lt;T&gt;</c>, turning the decorator pipeline's runtime
/// <c>ResultFailureFactory</c> constraint into a build-time gate. The base also fails when an
/// Application assembly declares no handler at all, so a module dropping out of the map cannot leave
/// the rule passing vacuously.
/// </summary>
public sealed class HandlerResultConventionTests : HandlerResultConventionTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}
