using Microsoft.Extensions.DependencyInjection;

namespace MMCA.Helpdesk.Tickets.Infrastructure;

/// <summary>
/// Registers the Tickets module infrastructure layer. It registers no service today, and the call
/// is still load-bearing: the framework discovers EF entity configurations by scanning the
/// assemblies ALREADY LOADED into the process, and the compiler drops an assembly reference no code
/// uses. This call is the code that makes the API assembly reference, and so the host load, this
/// assembly; without it the model would silently map no ticket table. Register external clients or
/// hosted services here as the module grows.
/// </summary>
public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddModuleTicketsInfrastructure() => services;
    }
}
