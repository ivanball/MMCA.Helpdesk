using System.Globalization;
using MMCA.Common.Application.UseCases.Markers;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tickets;

/// <summary>
/// The one place the Tickets module names its cache entries. Both halves of the framework's caching
/// pair read from here, and that is the point: <see cref="ICacheInvalidating.CachePrefix"/> on a
/// command and <see cref="IQueryCacheable.CacheKey"/> on a query are matched by string prefix, with
/// nothing at compile time to catch a mismatch. A read whose key does not start with
/// <see cref="Prefix"/> is simply never evicted, and the defect surfaces later as a stale ticket
/// rather than as a failure at the write.
/// </summary>
public static class TicketCacheKeys
{
    /// <summary>
    /// The prefix every ticket cache entry shares, and the prefix every ticket command invalidates.
    /// Derived from the aggregate's type name so two modules cannot collide on a shared cache.
    /// </summary>
    public static string Prefix { get; } = $"{typeof(Ticket).FullName}:";

    /// <summary>
    /// The cache key for a single-ticket read. Every input that changes the result belongs in the
    /// key: this read is keyed by id alone because the query has no other parameter.
    /// </summary>
    /// <param name="id">The ticket identifier.</param>
    /// <returns>A cache key under <see cref="Prefix"/>.</returns>
    public static string ById(TicketIdentifierType id) =>
        string.Create(CultureInfo.InvariantCulture, $"{Prefix}ById:{id}");
}
