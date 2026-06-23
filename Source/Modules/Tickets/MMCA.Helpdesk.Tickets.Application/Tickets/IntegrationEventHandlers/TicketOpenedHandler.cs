using Microsoft.Extensions.Logging;
using MMCA.Common.Application.Interfaces;
using MMCA.Helpdesk.Tickets.Shared.Tickets.IntegrationEvents;

namespace MMCA.Helpdesk.Tickets.Application.Tickets.IntegrationEventHandlers;

/// <summary>
/// Consumes <see cref="TicketOpenedIntegrationEvent"/>. In this monolith seed it just logs; in a real
/// system this is where a notification/email/analytics side effect would live. The same handler runs
/// in-process now and over the broker once the Tickets module is extracted (ADR-003 / ADR-008).
/// Auto-discovered by Scrutor (singleton lifetime); the dispatcher routes the outbox-published event here.
/// </summary>
public sealed partial class TicketOpenedHandler(ILogger<TicketOpenedHandler> logger)
    : IIntegrationEventHandler<TicketOpenedIntegrationEvent>
{
    public Task HandleAsync(TicketOpenedIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        LogTicketOpened(logger, integrationEvent.TicketId, integrationEvent.RequesterUserId, integrationEvent.SchemaVersion);

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Integration event: ticket {TicketId} opened by user {RequesterUserId} (schema v{SchemaVersion}).")]
    private static partial void LogTicketOpened(ILogger logger, int ticketId, int requesterUserId, int schemaVersion);
}
