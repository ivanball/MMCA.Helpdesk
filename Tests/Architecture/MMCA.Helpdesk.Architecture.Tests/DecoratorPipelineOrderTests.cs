using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.FeatureManagement;
using MMCA.Common.Application;
using MMCA.Common.Application.Interfaces;
using MMCA.Common.Application.Interfaces.Infrastructure;
using MMCA.Common.Application.Settings;
using MMCA.Common.Application.UseCases;
using MMCA.Common.Shared.Abstractions;
using MMCA.Common.Shared.Auth;
using MMCA.Common.Testing;
using MMCA.Helpdesk.Tickets.Application;
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.GetById;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;
using Moq;

namespace MMCA.Helpdesk.Architecture.Tests;

/// <summary>
/// Runtime fitness function for the ADR-014 decorator pipeline, exercised against a real Tickets
/// command/query pair (the framework's generic update command over the Ticket aggregate, and
/// <see cref="GetTicketByIdQuery"/>): it
/// builds this seed's genuine registration sequence (the module registration first,
/// <c>AddApplicationDecorators()</c> LAST, which is the one load-bearing ordering in the host) and
/// asserts the resolved object graph nests the decorators in exactly the documented order.
/// <para>
/// The seed's host wires that sequence in <c>Web/Program.cs</c>, where nothing but this test would
/// notice a reordered line or a module scan that drifted below the decorator call: Scrutor's
/// <c>TryDecorate</c> applies decorators in reverse registration order, so the damage is silent.
/// </para>
/// </summary>
public sealed class DecoratorPipelineOrderTests
    : DecoratorPipelineOrderTestsBase<
        UpdateEntityCommand<Ticket, TicketUpdateRequest, TicketIdentifierType>,
        Result<TicketDTO>,
        GetTicketByIdQuery,
        Result<TicketDTO>>
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Test doubles for the decorator constructor dependencies. Registered first so the real
        // sequence below can still override any of them.
        services.AddSingleton(Mock.Of<IFeatureManager>());
        services.AddSingleton(Mock.Of<ICorrelationContext>());
        services.AddSingleton(Mock.Of<ICacheService>());
        services.AddScoped(_ => Mock.Of<IUnitOfWork>());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(Mock.Of<ICurrentUserService>());
        services.AddSingleton(Mock.Of<IPermissionRegistry>());

        // The real registration sequence, mirroring the host: the Tickets module's own registration
        // first (its convention scan registers GetTicketByIdHandler, the update applier and the DTO
        // mappers, and AddEntityCrud closes the generic write side over them), decorators last.
        services.AddApplication();
        services.AddModuleTicketsApplication(new ApplicationSettings());
        services.AddApplicationDecorators();
    }
}
