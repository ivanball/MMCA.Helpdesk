namespace MMCA.Helpdesk.Architecture.Tests.Layering;

/// <summary>
/// Pins every <c>ProjectReference</c> between two different modules, driven by the shared
/// <see cref="CrossModuleReferenceTestsBase"/>. Helpdesk has one module (Tickets), so the allowlist is empty:
/// the fact still runs, confirming the module found on disk matches the architecture map, and a second
/// module scaffolded from the template starts with its cross-module references declared here.
/// </summary>
public sealed class CrossModuleReferenceTests : CrossModuleReferenceTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();

    protected override string RepoRoot { get; } = ArchitectureMapBase.FindRepoRoot("MMCA.Helpdesk.slnx");

    protected override IReadOnlyCollection<string> AllowedCrossModuleReferences => [];
}
