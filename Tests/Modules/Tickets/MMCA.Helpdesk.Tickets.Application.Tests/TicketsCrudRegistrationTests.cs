using AwesomeAssertions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MMCA.Common.Application.Interfaces;
using MMCA.Common.Application.Settings;
using MMCA.Common.Application.UseCases;
using MMCA.Common.Application.Validation;
using MMCA.Common.Shared.Abstractions;
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Create;
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Update;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tests;

/// <summary>
/// Pins the generic write-side wiring for the Ticket aggregate. Every assertion here guards a
/// registration that is invisible to a compiler and only surfaces at host start-up or, worse, on the
/// first request that needs it.
/// </summary>
public sealed class TicketsCrudRegistrationTests
{
    private static readonly ServiceCollection Registrations = BuildRegistrations();

    /// <summary>
    /// <c>AddEntityCrud</c> closes the framework's update handler over the module's own types, so the
    /// controller can inject one command handler and the module writes no update handler of its own
    /// beyond the eager-load subclass asserted below.
    /// </summary>
    [Fact]
    public void AddEntityCrud_Registers_TheGenericUpdateHandler() =>
        Registrations.Should().Contain(d => d.ServiceType == typeof(
            ICommandHandler<UpdateEntityCommand<Ticket, TicketUpdateRequest, TicketIdentifierType>, Result<TicketDTO>>));

    /// <inheritdoc cref="AddEntityCrud_Registers_TheGenericUpdateHandler"/>
    [Fact]
    public void AddEntityCrud_Registers_TheGenericDeleteHandler() =>
        Registrations.Should().Contain(d => d.ServiceType == typeof(
            ICommandHandler<DeleteEntityCommand<Ticket, TicketIdentifierType>, Result>));

    /// <summary>
    /// The applier is what keeps the field names out of the framework handler, and it is picked up by
    /// the module scan rather than registered by hand: a renamed or moved applier would leave the
    /// update verb resolving to nothing.
    /// </summary>
    [Fact]
    public void ModuleScan_Registers_TheUpdateApplier() =>
        Registrations.Should().Contain(d => d.ServiceType == typeof(
            IEntityUpdateApplier<Ticket, TicketUpdateRequest, TicketIdentifierType>));

    /// <summary>
    /// The framework's automatic bridge only sees commands declared in the module assembly, and the
    /// generic update command is declared in MMCA.Common, so the module registers the bridge itself.
    /// Without it the validating decorator would find no validator and
    /// <see cref="TicketUpdateRequestValidator"/> would silently stop running.
    /// </summary>
    [Fact]
    public void Module_Registers_TheUpdateRequestValidatorBridge() =>
        Registrations.Should().Contain(d =>
            d.ServiceType == typeof(IValidator<UpdateEntityCommand<Ticket, TicketUpdateRequest, TicketIdentifierType>>)
            && d.ImplementationType != null
            && d.ImplementationType.IsGenericType
            && d.ImplementationType.GetGenericTypeDefinition() == typeof(CommandRequestValidator<,>));

    /// <summary>
    /// <c>AddEntityCrud</c> registers all three verbs with <c>TryAdd</c> and runs after the module
    /// scan, so the module's own create handler (the one that publishes the integration event after
    /// the commit) keeps the create verb. Moving the call above the scan would silently hand creates
    /// to the framework's generic handler instead.
    /// </summary>
    [Fact]
    public void AddEntityCrud_DoesNotDisplace_TheModulesOwnCreateHandler() =>
        Registrations.Should().Contain(d =>
            d.ServiceType == typeof(ICommandHandler<TicketCreateRequest, Result<TicketDTO>>)
            && d.ImplementationType == typeof(CreateTicketHandler));

    // template:begin child
    /// <summary>
    /// Same ordering rule, for the update verb: the DTO a ticket update answers with carries the
    /// comments, so the module subclasses the framework handler to declare the eager load, and the
    /// scan has to register it AHEAD of <c>AddEntityCrud</c> for that subclass to survive.
    /// </summary>
    [Fact]
    public void AddEntityCrud_DoesNotDisplace_TheModulesOwnUpdateHandler() =>
        Registrations.Should().Contain(d =>
            d.ServiceType == typeof(
                ICommandHandler<UpdateEntityCommand<Ticket, TicketUpdateRequest, TicketIdentifierType>, Result<TicketDTO>>)
            && d.ImplementationType == typeof(TicketUpdateHandler));

    // template:end child
    private static ServiceCollection BuildRegistrations()
    {
        var services = new ServiceCollection();
        services.AddModuleTicketsApplication(new ApplicationSettings());
        return services;
    }
}
