// Each fitness-function test class is a thin subclass of a framework base, supplying the repo map.
// The rule bodies live once in MMCA.Common.Testing.Architecture and run identically across repos.
namespace MMCA.Helpdesk.Architecture.Tests;

public sealed class LayerDependencyTests : LayerDependencyTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}

public sealed class DomainPurityTests : DomainPurityTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}

public sealed class ModuleIsolationTests : ModuleIsolationTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}

public sealed class SharedLayerTests : SharedLayerTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}

// Opt into the cross-source specification rule: a spec must filter on its entity's own columns, not
// navigate to another entity (which would not translate if that entity ever lives in a different data
// source). Use CrossSourceSpecification for cross-source filters. Vacuous today (Tickets has no specs),
// but keeps the reference app consistent and guards future specs.
public sealed class SpecificationConventionTests : SpecificationConventionTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}
