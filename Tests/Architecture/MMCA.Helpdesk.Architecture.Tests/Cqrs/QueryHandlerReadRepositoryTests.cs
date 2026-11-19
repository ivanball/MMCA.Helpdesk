namespace MMCA.Helpdesk.Architecture.Tests.Cqrs;

/// <summary>
/// Read-side purity guard, driven by the shared <see cref="QueryHandlerReadRepositoryTestsBase"/>:
/// every <c>IQueryHandler</c> in Helpdesk's module Application assemblies takes
/// <c>IUnitOfWork.GetReadRepository</c> and never <c>IUnitOfWork.GetRepository</c>, so a query is
/// never handed the add, remove and tracked-read surface it must not use.
/// <para>
/// The allowlist is deliberately empty, so the first query handler that reaches for the write
/// repository fails here rather than being absorbed by a list.
/// </para>
/// </summary>
public sealed class QueryHandlerReadRepositoryTests : QueryHandlerReadRepositoryTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();
}
