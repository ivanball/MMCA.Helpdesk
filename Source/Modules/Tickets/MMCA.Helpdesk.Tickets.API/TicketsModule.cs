using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MMCA.Common.Application.Modules;
using MMCA.Common.Application.Settings;

namespace MMCA.Helpdesk.Tickets.API;

/// <summary>
/// Tickets module entry point discovered and registered by the framework's <c>ModuleLoader</c>.
/// A leaf module: no dependencies, so it relies on the default <c>Dependencies</c> /
/// <c>RequiresDependencies</c> / <c>RegisterDisabledStubs</c> from <see cref="IModule"/>.
/// </summary>
public sealed class TicketsModule : IModule
{
    public string Name => "Tickets";

    public void Register(IServiceCollection services, IConfigurationBuilder configuration, ApplicationSettings applicationSettings) =>
        services.AddTicketsModule(applicationSettings);
}
