namespace MMCA.Helpdesk.Architecture.Tests.Cqrs;

/// <summary>
/// Domain-event-handler purity guard, driven by the shared <see cref="DomainEventHandlerSaveTestsBase"/>:
/// a handler mutates state and lets the owning unit of work flush it, because dispatch runs after
/// <c>SaveChangesAsync</c> (and after commit inside an <c>ITransactional</c> command), so a handler
/// that saves starts a second write inside the first one. The rule walks the call graph transitively
/// out of every handler.
/// <para>
/// <see cref="DomainEventHandlerSaveTestsBase.AllowedSavingTypes"/> is deliberately not overridden,
/// so it keeps the base's single <c>MMCA.Common</c> entry (the framework's outbox event bus persists
/// by design, the same entry Store and ADC carry). A module-specific entry would name this module's
/// types and cover nothing in a module an adopter scaffolds, so the first saving handler fails here.
/// </para>
/// </summary>
public sealed class DomainEventHandlerSaveTests : DomainEventHandlerSaveTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}
