using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using MMCA.Common.Application.Interfaces.Events;
using MMCA.Common.Application.Webhooks;
using MMCA.Helpdesk.Tickets.Application.Tickets.IntegrationEventHandlers;
using MMCA.Helpdesk.Tickets.Shared.Tickets.IntegrationEvents;

namespace MMCA.Helpdesk.Tickets.Application.Tests.Webhooks;

/// <summary>
/// Pins the module half of the outbound-webhook worked example (item 35): the Application
/// registration publishes the EXISTING <see cref="TicketOpenedIntegrationEvent"/> as the webhook event
/// type <c>ticket.opened</c>, with no new event and no change to the module's own handler. The fan-out
/// handler sits beside that handler and is inert in any host that does not also call
/// <c>AddWebhooks(configuration)</c>, so the registration is safe in every module the template
/// generates.
/// </summary>
public sealed class TicketWebhookEventTests
{
    private const string WebhookEventType = "ticket.opened";

    [Fact]
    public void AddModuleTicketsApplication_RegistersTicketOpened_AsTicketDotOpened()
    {
        using var provider = BuildRegistrations().BuildServiceProvider();
        var catalog = provider.GetRequiredService<WebhookEventCatalog>();

        catalog.TryGetEventType(typeof(TicketOpenedIntegrationEvent), out var eventType).Should().BeTrue(
            "the module publishes its opened event to webhook subscribers");
        eventType.Should().Be(WebhookEventType);
        catalog.Contains(WebhookEventType).Should().BeTrue();
    }

    [Fact]
    public void AddModuleTicketsApplication_AddsTheWebhookFanOut_BesideTheModulesOwnHandler()
    {
        var implementations = BuildRegistrations()
            .Where(descriptor => descriptor.ServiceType == typeof(IIntegrationEventHandler<TicketOpenedIntegrationEvent>))
            .Select(descriptor => descriptor.ImplementationType)
            .ToList();

        implementations.Should().Contain(
            typeof(TicketOpenedHandler),
            "the module's own handler keeps running unchanged");
        implementations.Should().ContainSingle(
            type => type != null && type.IsGenericType && type.Name.StartsWith("WebhookFanOutHandler", StringComparison.Ordinal),
            "exactly one webhook fan-out handler is closed over the event");
    }

    // The registration method takes no settings parameter once item 2 lands (the owner ruling deletes
    // the unused one); the existing tests in this project still pass one and change with that item.
    private static ServiceCollection BuildRegistrations()
    {
        var services = new ServiceCollection();
        services.AddModuleTicketsApplication();
        return services;
    }
}
