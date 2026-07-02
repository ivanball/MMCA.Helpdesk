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

// Gate the ADR-035 optimistic-concurrency round-trip: every *UpdateRequest must implement
// IConcurrencyAware. Non-vacuous since TicketUpdateRequest adopted the seam (drift plan H2).
public sealed class ConcurrencyConventionTests : ConcurrencyConventionTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}

// The remaining applicable shared rules, adopted for full consumer parity with ADC/Store (drift
// plan H1): naming, handler, controller, entity, and immutability conventions, the frozen
// integration-event wire contract, the transport-at-the-edge extraction rules, the PII/erasure
// convention, slice cohesion, and the constructor-dependency ceiling.
public sealed class NamingConventionTests : NamingConventionTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}

public sealed class HandlerConventionTests : HandlerConventionTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}

public sealed class ControllerConventionTests : ControllerConventionTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}

public sealed class EntityConventionTests : EntityConventionTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}

public sealed class ImmutabilityTests : ImmutabilityTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}

public sealed class IntegrationEventContractTests : IntegrationEventContractTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();

    // Frozen wire contract for the module's async API. Update DELIBERATELY when evolving an
    // integration event (version it per ADR-010; never a silent reshape).
    protected override IReadOnlyList<string> ExpectedContract =>
    [
        "MMCA.Helpdesk.Tickets.Shared.Tickets.IntegrationEvents.TicketOpenedIntegrationEvent { RequesterUserId:Int32, TicketId:Int32 }",
    ];
}

public sealed class MicroserviceExtractionTests : MicroserviceExtractionTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}

public sealed class PiiConventionTests : PiiConventionTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}

public sealed class SliceCohesionTests : SliceCohesionTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}

// NOT adopted (legitimately inapplicable, not an enforcement gap): ConstructorDependencyCountTestsBase
// scans Application *Service classes and deliberately fails when it finds none (anti-vacuity guard).
// This seed's Application layer is handlers-only. Subclass it (ceiling 7, matching ADC) as soon as the
// first Application *Service appears. LocalizationResource/DataResidency/BrandColorToken/FormsConvention
// stay N/A for the same reduced-scope reasons (single-locale, single-region, no branded CSS, MudForm-less).
