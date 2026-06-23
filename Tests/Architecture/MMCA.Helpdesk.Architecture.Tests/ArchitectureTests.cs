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
