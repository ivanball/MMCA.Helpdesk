namespace MMCA.Helpdesk.Architecture.Tests;

/// <summary>
/// <c>[ServiceContract]</c> encapsulation rule (the class serving a published contract must not be
/// public, so a consumer cannot reference, construct or subclass it), driven by the shared
/// <see cref="ContractImplementationTestsBase"/>. The twin of <see cref="ServiceContractPurityTests"/>:
/// purity keeps the producer's internals out of the contract, this keeps the contract's implementation
/// out of the consumer's reach, which is what lets the interface later be answered over a wire.
/// Vacuously green today (this seed marks no interface), and that is the point: the ratchet is in place
/// before the first contract appears, so there is no test to remember to write then.
/// </summary>
public sealed class ContractImplementationTests : ContractImplementationTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}
