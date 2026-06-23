using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MMCA.Common.Application;
using MMCA.Common.Application.Interfaces;
using MMCA.Common.Application.Services;
using MMCA.Common.Application.Settings;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Tickets.Application;

/// <summary>
/// Registers the Tickets module application layer: the entity query service, a navigation populator,
/// and the convention-scanned handlers, mappers, and validators.
/// </summary>
public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddModuleTicketsApplication(ApplicationSettings applicationSettings)
        {
            _ = applicationSettings; // Reserved for future decorator configuration.

            // The Ticket aggregate has children but eager loading goes through repository includes,
            // so a null populator suffices here (swap for a custom INavigationPopulator<Ticket> if the
            // query service needs to batch-load comments).
            services.TryAddScoped<INavigationPopulator<Ticket>, NullNavigationPopulator<Ticket>>();
            services.TryAddScoped<IEntityQueryService<Ticket, TicketDTO, TicketIdentifierType>,
                EntityQueryService<Ticket, TicketDTO, TicketIdentifierType>>();

            // Convention scan: command/query handlers, DTO/request mappers, validators, event handlers.
            services.ScanModuleApplicationServices<ClassReference>();

            return services;
        }
    }
}
