using Microsoft.Extensions.Logging;
using MMCA.Common.Application.Interfaces;
using MMCA.Common.Domain.Enums;
using MMCA.Helpdesk.Tickets.Domain.Tickets.DomainEvents;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.DomainEventHandlers;

/// <summary>
/// In-process consumer of the <see cref="TicketChanged"/> domain event: writes an audit log line on
/// every ticket mutation (Updated / Deleted). Demonstrates the intra-module domain-event path: raised
/// via <c>AddDomainEvent</c> on the aggregate and dispatched by the <c>DomainEventDispatcher</c> after
/// <c>SaveChangesAsync</c>, within the same transaction. Auto-discovered by Scrutor.
/// Creation is audited separately by the integration-event consumer (<c>TicketOpenedHandler</c>),
/// because the aggregate omits the Added domain event (DB-generated id).
/// </summary>
public sealed partial class TicketChangedAuditHandler(ILogger<TicketChangedAuditHandler> logger)
    : IDomainEventHandler<TicketChanged>
{
    public Task HandleAsync(TicketChanged domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        LogTicketChanged(logger, domainEvent.TicketId, domainEvent.State);

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Audit: ticket {TicketId} {State}.")]
    private static partial void LogTicketChanged(ILogger logger, int ticketId, DomainEntityState state);
}
