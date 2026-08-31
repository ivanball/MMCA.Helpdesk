using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MMCA.Common.Application;
using MMCA.Common.Application.Interfaces;
using MMCA.Common.Application.Services;
using MMCA.Common.Application.Settings;
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Create;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Tickets.Application;

/// <summary>
/// Registers the Tickets module application layer: the entity query service, a navigation populator,
/// the convention-scanned handlers, mappers, and validators, and the framework's generic write side.
/// </summary>
public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddModuleTicketsApplication(ApplicationSettings applicationSettings)
        {
            _ = applicationSettings; // Reserved for future decorator configuration.

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
            // Deliberately AFTER the scan, because AddEntityCrud registers with TryAdd and the module
            // keeps the create verb for itself: CreateTicketHandler publishes
            // TicketOpenedIntegrationEvent after the commit.
            // template:begin child
            // The update verb too: TicketUpdateHandler eager-loads the children the response carries.
            // template:end child
            // Moving this call above the scan would hand those verbs to the plain generic handlers,
            // silently.
            //
            // The call also registers the CommandRequestValidator bridge for the closed
            // UpdateEntityCommand<,,>, which is what keeps TicketUpdateRequestValidator running in the
            // validating decorator: the scan's own bridge only sees commands declared in THIS
            // assembly, and that command is declared in MMCA.Common.
            services.AddEntityCrud<Ticket, TicketDTO, TicketIdentifierType, TicketCreateRequest, TicketUpdateRequest>();

            return services;
        }
    }
}
