namespace MMCA.Helpdesk.Architecture.Tests.Cqrs;

/// <summary>
/// Vertical-slice cohesion rules, driven by the shared <see cref="SliceCohesionTestsBase"/>: every
/// module's <c>Application/{Aggregate}/UseCases/{Operation}/</c> slice keeps its command or query,
/// handler and validator in one namespace, so a handler is never stranded from its contract. This is
/// the slice shape the <c>mmca-command</c> and <c>mmca-query</c> templates generate, so the rule also
/// keeps the seed and the templates in step.
/// </summary>
public sealed class SliceCohesionTests : SliceCohesionTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}
