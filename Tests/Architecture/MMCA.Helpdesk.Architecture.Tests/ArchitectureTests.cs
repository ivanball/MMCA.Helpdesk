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

// Gate the integration-event schema-versioning convention (ADR-010): every concrete integration event
// inherits BaseIntegrationEvent, declares an int SchemaVersion, and lives in a *.IntegrationEvents
// namespace in the Shared layer. Tickets ships TicketOpenedIntegrationEvent, so this is non-vacuous and
// closes the enforcement gap (the rule was previously only subclassed in ADC and Store).
public sealed class EventConventionTests : EventConventionTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}

// Gate the ADR-016 lockstep invariant this seed participates in: all MMCA.Common.* pins in
// Directory.Packages.props must share one version, so a partial sweep is caught at build time even in
// this reference app (which defaults to local-source mode but keeps package-mode pins for consumers
// who delete local.props).
public sealed class FrameworkVersionConsistencyTests : FrameworkVersionConsistencyTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}
