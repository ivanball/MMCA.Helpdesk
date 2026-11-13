using Microsoft.Extensions.DependencyInjection;
using MMCA.Helpdesk.Tickets.Application;
using MMCA.Helpdesk.Tickets.Infrastructure;

namespace MMCA.Helpdesk.Tickets.API;

/// <summary>
/// Composes the Tickets module across its Application and Infrastructure layers. The API layer itself
/// registers nothing: its controllers are discovered by MVC.
/// </summary>
public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddTicketsModule()
        {
            services.AddModuleTicketsApplication();
            services.AddModuleTicketsInfrastructure();

            return services;
        }
    }
}
