namespace MMCA.Helpdesk.Architecture.Tests.Domain;

/// <summary>
/// Cascade-soft-delete convention (ADR-005), driven by the shared
/// <see cref="CascadeSoftDeleteConventionTestsBase"/>: a soft-delete is an ordinary UPDATE, so it
/// does not cascade on its own. Every Helpdesk aggregate root that owns a collection of auditable
/// children must delete them in its own <c>Delete()</c> override, through
/// <c>DeleteChildren&lt;TChild, TChildId&gt;(...)</c> or by calling each child's <c>Delete()</c>;
/// otherwise the root vanishes behind the global query filter while its child rows stay active for
/// exports, reports and erasure requests.
/// <para>
/// The exemption list is deliberately empty: the module's aggregate cascades to its children, and an
/// adopter's first child-bearing aggregate has to do the same or record why its children outlive it.
/// </para>
/// </summary>
public sealed class CascadeSoftDeleteConventionTests : CascadeSoftDeleteConventionTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}
