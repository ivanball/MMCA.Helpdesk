namespace MMCA.Helpdesk.Architecture.Tests;

/// <summary>
/// Anonymous-endpoint gate: every <c>[AllowAnonymous]</c> in this app's own presentation layer must
/// be a reviewed entry in <see cref="AllowedAnonymousEndpoints"/>, so an endpoint cannot lose its
/// authorization gate unnoticed. The rule bodies live once in MMCA.Common.Testing.Architecture; this
/// subclass supplies only the assemblies, the allow-list, and the non-vacuity floor.
/// <para>
/// The scan covers this app's own API assembly, not the framework's: MMCA.Common gates its own
/// anonymous surface (the credential-exchange actions) in its own subclass, and the base reads
/// attributes DeclaredOnly, so an inherited framework action is never re-reported here. The Blazor
/// host under Source/Hosts/UI is out of scope because this test project references the module layers
/// only, matching every other fitness function here; its pages carry no attribute-based
/// authorization in this seed.
/// </para>
/// </summary>
public sealed class AnonymousEndpointTests : AnonymousEndpointTestsBase
{
    protected override IReadOnlyCollection<Assembly> TargetAssemblies =>
    [
        typeof(MMCA.Helpdesk.Tickets.API.Controllers.TicketsController).Assembly,
    ];

    protected override IReadOnlyCollection<string> AllowedAnonymousEndpoints =>
    [
        // The whole controller is anonymous on purpose: this monolith seed ships WITHOUT an Identity
        // issuer, so there is no token to require (README.md and CLAUDE.md call out the issuer-less
        // default). Adding the Identity module (GETTING-STARTED.md Phase 8) means flipping the type
        // to [Authorize] and deleting this entry.
        "MMCA.Helpdesk.Tickets.API.Controllers.TicketsController",
    ];

    // The exact count today: one controller type and no routable components in the API assembly. A
    // renamed or missing assembly would make the allow-list check vacuous, so the floor fails first.
    protected override int MinimumScannedTypes => 1;
}
