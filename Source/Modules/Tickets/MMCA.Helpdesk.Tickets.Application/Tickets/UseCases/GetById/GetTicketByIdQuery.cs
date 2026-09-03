using MMCA.Common.Application.UseCases.Markers;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.GetById;

/// <summary>
/// Query for a single ticket including its (non-deleted) children.
/// <para>
/// This is the read half of the framework's caching pair. Implementing
/// <see cref="IQueryCacheable"/> puts the query behind <c>CachingQueryDecorator</c>: the first call
/// runs the handler and stores the result, later calls are served from the cache without touching
/// the database, and every ticket command (each implements <see cref="ICacheInvalidating"/> over
/// <see cref="TicketCacheKeys.Prefix"/>) evicts the entry after it succeeds. Without a cacheable
/// read on the other side those commands invalidate nothing, which is exactly how an extension
/// point ends up shipped but undemonstrated.
/// </para>
/// <para>
/// The duration is a staleness budget, not a performance knob. A write that goes through the
/// pipeline evicts the entry immediately, so the budget only bounds the window for changes that
/// bypass the pipeline (a migration, a manual edit, another writer against the same database).
/// </para>
/// </summary>
/// <param name="Id">The ticket identifier.</param>
public sealed record GetTicketByIdQuery(TicketIdentifierType Id) : IQueryCacheable
{
    /// <inheritdoc />
    public string CacheKey => TicketCacheKeys.ById(Id);

    /// <inheritdoc />
    public TimeSpan CacheDuration => TimeSpan.FromMinutes(5);
}
