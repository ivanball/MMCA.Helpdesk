namespace MMCA.Helpdesk.Architecture.Tests.Domain;

/// <summary>
/// Result-pattern purity guard (ADR-013), driven by the shared <see cref="DomainThrowTestsBase"/>:
/// the Helpdesk domain returns <c>Result</c> for business outcomes rather than throwing, so a thrown
/// business failure cannot skip <c>Result.Combine</c> invariant composition or surface as a 500.
/// Argument guards stay allowed, because they report a caller bug rather than a business outcome.
/// <para>
/// The allowlist is deliberately empty, so the first non-guard throw fails here rather than being
/// absorbed by a list.
/// </para>
/// </summary>
public sealed class DomainThrowTests : DomainThrowTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}
