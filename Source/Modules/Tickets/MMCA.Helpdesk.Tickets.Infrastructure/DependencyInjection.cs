using Microsoft.Extensions.DependencyInjection;

namespace MMCA.Helpdesk.Tickets.Infrastructure;

/// <summary>
/// Registers the Tickets module infrastructure layer. EF entity configurations are auto-discovered
/// by assembly-name convention, so this is currently a no-op; register external clients or hosted
/// services here as the module grows.
/// </summary>
public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddModuleTicketsInfrastructure() => services;
    }
}
