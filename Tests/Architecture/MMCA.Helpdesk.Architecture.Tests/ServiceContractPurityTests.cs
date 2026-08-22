namespace MMCA.Helpdesk.Architecture.Tests;

/// <summary>
/// <c>[ServiceContract]</c> purity rule (a published contract type must not reach into the producing
/// service's Domain, Application or Infrastructure), driven by the shared
/// <see cref="ServiceContractPurityTestsBase"/>. Attribute-driven ratchet: it scans every assembly the
/// map registers and enforces the invariant from the first marked type onward.
/// </summary>
public sealed class ServiceContractPurityTests : ServiceContractPurityTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}
