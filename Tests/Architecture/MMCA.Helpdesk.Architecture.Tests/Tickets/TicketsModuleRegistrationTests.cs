using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MMCA.Common.Application.Settings;
using MMCA.Common.Infrastructure.Persistence;
using MMCA.Helpdesk.Tickets.API;
using MMCA.Helpdesk.Tickets.API.Controllers;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Architecture.Tests.Tickets;

/// <summary>
/// Pins the module's registration path after the empty API registration hop and the unused settings
/// parameter are gone (item 2): everything a request needs is still registered through
/// <see cref="TicketsModule.Register"/>, the one entry point <c>ModuleLoader</c> calls, and the
/// Infrastructure hop the owner kept still loads the assembly the entity configurations live in.
/// <para>
/// The Infrastructure hop is load-bearing for a reason no compiler sees: the framework's
/// <see cref="DefaultEntityConfigurationAssemblyProvider"/> finds entity configurations by scanning the
/// assemblies ALREADY LOADED into the process, and the C# compiler drops an assembly reference no code
/// uses. Were the hop deleted, the API assembly would stop referencing the Infrastructure assembly, the
/// host would never load it, and the model would silently map no ticket table.
/// </para>
/// </summary>
public sealed class TicketsModuleRegistrationTests
{
    private const string InfrastructureAssemblyName = "MMCA.Helpdesk.Tickets.Infrastructure";

    [Fact]
    public void ModuleRegister_RegistersEveryDependencyTheTicketsControllerTakes()
    {
        var services = RegisterThroughTheModule();

        var missing = typeof(TicketsController).GetConstructors()
            .Should().ContainSingle("the controller declares one primary constructor").Which
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .Where(type => !(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ILogger<>)))
            .Where(type => !services.Any(descriptor => descriptor.ServiceType == type))
            .Select(type => type.ToString())
            .ToList();

        missing.Should().BeEmpty(
            "every handler and query service a ticket request reaches must be registered by the module's own Register: {0}",
            string.Join("; ", missing));
    }

    [Fact]
    public void ApiAssembly_StillReferencesTheInfrastructureAssembly()
    {
        var referenced = typeof(TicketsModule).Assembly.GetReferencedAssemblies()
            .Select(name => name.Name)
            .ToList();

        referenced.Should().Contain(
            InfrastructureAssemblyName,
            "the Infrastructure registration hop is the only code that makes the host load that assembly, and the entity configuration scan only sees loaded assemblies");
    }

    [Fact]
    public void ModuleRegister_LeavesTheTicketConfigurationDiscoverable()
    {
        _ = RegisterThroughTheModule();
        var provider = new DefaultEntityConfigurationAssemblyProvider(Options.Create(new EntityConfigurationOptions()));

        var configurationAssembly = provider.GetConfigurationAssemblies()
            .Should().Contain(
                assembly => assembly.GetName().Name == InfrastructureAssemblyName,
                "the framework discovers the module's entity configurations by scanning loaded Infrastructure assemblies")
            .Which;

        configurationAssembly.GetTypes()
            .Should().Contain(
                type => !type.IsAbstract
                    && type.BaseType != null
                    && type.BaseType.IsGenericType
                    && type.BaseType.GetGenericArguments().Contains(typeof(Ticket)),
                "the ticket table is mapped by a configuration in that assembly");
    }

    [Fact]
    public void ApiAssembly_DeclaresNoEmptyApiRegistrationMethod()
    {
        var methods = AllDeclaredMethods(typeof(TicketsModule).Assembly)
            .Where(method => method.Name == "AddModuleTicketsAPI")
            .Select(method => $"{method.DeclaringType}.{method.Name}")
            .ToList();

        methods.Should().BeEmpty(
            "the API layer registers nothing, so the empty registration method is deleted rather than carried into every generated module");
    }

    // DESIGN-QUESTION: the owner ruling names "the unused settings parameter" in the singular. This pins
    // the Application-layer one (the only parameter that is read nowhere today). Whether AddTicketsModule
    // in the API layer keeps its own parameter once it has nothing to forward is left to the implementer.
    [Fact]
    public void ApplicationRegistration_TakesNoSettingsParameter()
    {
        var registrations = AllDeclaredMethods(typeof(MMCA.Helpdesk.Tickets.Application.ClassReference).Assembly)
            .Where(method => method.Name == "AddModuleTicketsApplication")
            .ToList();

        registrations.Should().NotBeEmpty("the Application layer keeps its registration method");
        registrations
            .Where(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(ApplicationSettings)))
            .Select(method => method.ToString())
            .Should().BeEmpty("the settings parameter was reserved for a use that never came, so it is deleted");
    }

    /// <summary>
    /// Registers the module exactly as <c>ModuleLoader</c> does: through the <see cref="TicketsModule"/>
    /// entry point, with a settings instance and an empty configuration builder.
    /// </summary>
    /// <returns>The populated service collection.</returns>
    private static ServiceCollection RegisterThroughTheModule()
    {
        var services = new ServiceCollection();
        new TicketsModule().Register(services, new ConfigurationBuilder(), new ApplicationSettings());
        return services;
    }

    /// <summary>
    /// Every method declared on every type of an assembly, nested compiler-generated types included, so
    /// an extension-block member is found whichever of its two compiled forms carries it.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>The declared methods.</returns>
    private static IEnumerable<MethodInfo> AllDeclaredMethods(Assembly assembly) =>
        assembly.GetTypes().SelectMany(type => type.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly));
}
