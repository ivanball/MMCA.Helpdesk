namespace MMCA.Helpdesk.Architecture.Tests.Cqrs;

/// <summary>
/// Trailing-CancellationToken convention, driven by the shared
/// <see cref="CancellationTokenConventionTestsBase"/>: every public async method on a public
/// Application- or Infrastructure-layer type in Helpdesk declares a trailing
/// <c>CancellationToken cancellationToken</c>, so a cancelled request, an expired CQRS timeout budget
/// or a stopping host stops the work instead of leaving it running against the database.
/// <para>
/// <see cref="CancellationTokenConventionTestsBase.CancellationTokenExemptMethods"/> is deliberately
/// not overridden: the framework's own exemptions are inherited from the base, and an entry written
/// here would name this module's types and cover nothing in a module an adopter scaffolds. Add the
/// parameter instead.
/// </para>
/// </summary>
public sealed class CancellationTokenConventionTests : CancellationTokenConventionTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}
