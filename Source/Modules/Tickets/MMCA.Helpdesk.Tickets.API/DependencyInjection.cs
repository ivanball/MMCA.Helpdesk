using Microsoft.Extensions.DependencyInjection;
using MMCA.Common.Application.Settings;
using MMCA.Helpdesk.Tickets.Application;
using MMCA.Helpdesk.Tickets.Infrastructure;

namespace MMCA.Helpdesk.Tickets.API;

/// <summary>
/// Composes the Tickets module across its Application, Infrastructure, and API layers.
/// </summary>
public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddTicketsModule(ApplicationSettings applicationSettings)
        {
            services.AddModuleTicketsApplication(applicationSettings);
            services.AddModuleTicketsInfrastructure();
            services.AddModuleTicketsAPI();

            return services;
        }

        public IServiceCollection AddModuleTicketsAPI() => services;
    }
}
