namespace MMCA.Helpdesk.Architecture.Tests.Domain;

/// <summary>
/// Clock-as-input guard, driven by the shared <see cref="ClockReadTestsBase"/>: Helpdesk's Domain and
/// Application code takes the instant from an injected <see cref="TimeProvider"/> or as an argument to
/// the domain method, and never reads <c>DateTime.UtcNow</c> or its siblings, so every time-dependent
/// branch is testable without sleeping.
/// <para>
/// <see cref="ClockReadTestsBase.AllowedClockReaders"/> is deliberately not overridden: an entry names
/// a full type or member, so one written here would name this module's types and cover nothing in a
/// module an adopter scaffolds. Thread the instant through instead.
/// </para>
/// </summary>
public sealed class ClockReadTests : ClockReadTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}
