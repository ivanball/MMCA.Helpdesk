namespace MMCA.Helpdesk.Architecture.Tests.Domain;

/// <summary>
/// Soft-delete enforcement guard (ADR-005), driven by the shared
/// <see cref="SoftDeleteEnforcementTestsBase"/>: Helpdesk deletes by setting <c>IsDeleted = true</c>,
/// so EF Core's row-erasing members are banned outside the reviewed types the base allows. Every one
/// of those is an MMCA.Common framework type the base lists itself; no Helpdesk module or host erases
/// a row of its own, so this class adds no entries, and a generated app starts with an empty list.
/// </summary>
public sealed class SoftDeleteEnforcementTests : SoftDeleteEnforcementTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}
