using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MMCA.Common.Application;
using MMCA.Common.Application.Interfaces.Mapping;
using MMCA.Common.Application.Interfaces.Navigation;
using MMCA.Common.Application.Services;
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Create;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets.IntegrationEvents;

namespace MMCA.Helpdesk.Tickets.Application;

/// <summary>
/// Registers the Tickets module application layer: the entity query service, a navigation populator,
/// the convention-scanned handlers, mappers, and validators, and the framework's generic write side.
/// </summary>
public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddModuleTicketsApplication()
        {
            // Eager loading goes through repository includes, so a null populator suffices here
            // (swap for a custom INavigationPopulator<Ticket> if the query service ever needs to
            // batch-load child collections).
            services.TryAddScoped<INavigationPopulator<Ticket>, NullNavigationPopulator<Ticket>>();
            services.TryAddScoped<IEntityQueryService<Ticket, TicketDTO, TicketIdentifierType>,
                EntityQueryService<Ticket, TicketDTO, TicketIdentifierType>>();

            // Convention scan: command/query handlers, DTO/request mappers, update appliers,
            // validators, event handlers.
            services.ScanModuleApplicationServices<ClassReference>();

            // The framework's generic write side for the Ticket aggregate: one call closes the
            // create, update and delete handlers over the module's own types, and the mutation stays
            // on the aggregate, reached through the TicketUpdateApplier the scan picked up beside the
            // create mapper.
            //
            // The module keeps the create verb for itself: CreateTicketHandler publishes
            // TicketOpenedIntegrationEvent after the commit.
            // template:begin child
            // The update verb too: TicketUpdateHandler eager-loads the children the response carries.
            // template:end child
            // Call order does not decide that. The scan above appends its handlers, AddEntityCrud only
            // TryAdds its generic ones, and the container resolves the LAST registration for a
            // service, so the hand-written handlers win whichever of the two runs first. The real
            // hazard is a hand registration made with TryAdd AFTER this call: the verb is already
            // registered by then, so that TryAdd is a silent no-op and the generic handler keeps it.
            //
            // The call also registers the CommandRequestValidator bridge for the closed
            // UpdateEntityCommand<,,>, which is what keeps TicketUpdateRequestValidator running in the
            // validating decorator: the scan's own bridge only sees commands declared in THIS
            // assembly, and that command is declared in MMCA.Common.
            services.AddEntityCrud<Ticket, TicketDTO, TicketIdentifierType, TicketCreateRequest, TicketUpdateRequest>();

            // Outbound webhooks: the module publishes its existing opened event to subscribers as
            // ticket.opened, beside its own handler and with no change to it. Inert in a host that
            // does not call AddWebhooks(configuration) or leaves Webhooks:Enabled off.
            services.AddWebhookEvent<TicketOpenedIntegrationEvent>("ticket.opened");

            return services;
        }
    }
}
