using MMCA.Common.Application.UseCases.Markers;

namespace MMCA.Helpdesk.Tickets.Application.Tests.Caching;

/// <summary>
/// Builds one cache-invalidating command for <see cref="TicketCacheInvalidationTests"/>, which finds
/// every implementation in this assembly and adds it to the builders it checks with. This is how a
/// command that arrives through the mmca-command slice template is checked with no edit to the
/// test: the slice writes one of these beside its use case, in this project, staged from
/// <see cref="Tickets.UseCases.Delete.DeleteTicketCommandCacheBuilder"/>. A command written by hand
/// gets an entry in the test's own builder table or a builder of its own; one with neither fails the
/// test by name.
/// </summary>
public interface ICacheInvalidatingCommandBuilder
{
    /// <summary>Gets the exact command type <see cref="Build"/> returns.</summary>
    Type CommandType { get; }

    /// <summary>Builds an instance whose cache prefix the test checks.</summary>
    /// <returns>The command.</returns>
    ICacheInvalidating Build();
}
