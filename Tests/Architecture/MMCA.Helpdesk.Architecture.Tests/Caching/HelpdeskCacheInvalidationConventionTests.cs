using MMCA.Common.Application.UseCases.Crud;

namespace MMCA.Helpdesk.Architecture.Tests.Caching;

/// <summary>
/// Runs the shared cache-invalidation convention (<see cref="CacheInvalidationConventionTestsBase"/>)
/// over this app's Application assemblies, framework and module: every command that evicts cached
/// reads names its aggregate through <c>ICacheInvalidating&lt;TEntity&gt;</c> and states no prefix of
/// its own, so the key space it evicts is the one that aggregate's cached reads live under.
/// </summary>
public sealed class HelpdeskCacheInvalidationConventionTests : CacheInvalidationConventionTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();

    /// <summary>
    /// The framework's generic CRUD commands, which the map's framework Application assembly carries:
    /// they keep a settable <c>CachePrefix</c> on purpose, because the generic controller constructs
    /// them itself and the <see langword="init"/> setter is how a caller opts out of invalidation with
    /// an empty string. MMCA.Common allow-lists the same three in its own subclass.
    /// </summary>
    protected override IReadOnlyCollection<Type> AllowedCustomPrefixTypes =>
    [
        typeof(UpdateEntityCommand<,,>),
        typeof(UpdateEntityCommand<,,,>),
        typeof(DeleteEntityCommand<,>),
    ];
}
