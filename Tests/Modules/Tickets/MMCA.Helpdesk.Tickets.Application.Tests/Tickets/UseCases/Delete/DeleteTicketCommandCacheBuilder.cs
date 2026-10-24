using MMCA.Common.Application.UseCases.Markers;
using MMCA.Helpdesk.Tickets.Application.Tests.Caching;
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Delete;

namespace MMCA.Helpdesk.Tickets.Application.Tests.Tickets.UseCases.Delete;

/// <summary>
/// Hands <see cref="DeleteTicketCommand"/> to the cache-invalidation test. The mmca-command slice
/// template is staged from this use case and ships this file with it, renamed, so every command it
/// scaffolds is checked against the read prefix on arrival.
/// </summary>
public sealed class DeleteTicketCommandCacheBuilder : ICacheInvalidatingCommandBuilder
{
    /// <inheritdoc />
    public Type CommandType => typeof(DeleteTicketCommand);

    /// <inheritdoc />
    public ICacheInvalidating Build() => new DeleteTicketCommand(default);
}
