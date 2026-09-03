using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using MMCA.Common.Application;
using MMCA.Common.Application.Interfaces.Mapping;
using MMCA.Helpdesk.Tickets.Application.Tickets.DTOs;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;

namespace MMCA.Helpdesk.Tickets.Application.Tests.Projections;

/// <summary>
/// Pins the framework's projection contract: a registered
/// <see cref="IEntityDTOProjector{TEntity, TEntityDTO, TIdentifierType}"/> switches list reads onto
/// server-side projection, so the projected DTO MUST carry the same values the instance mapper
/// produces. The two paths are chosen by registration, not by the caller, so a divergence would make
/// a response depend on whether a projector happened to exist.
/// </summary>
public class TicketDTOProjectorTests
{
    [Fact]
    public void Projection_ProducesTheSameDTO_AsTheInstanceMapper()
    {
        var ticket = Ticket.Create(id: null, "Cannot log in", "The login page returns a 500.", requesterUserId: 42).Value!;
        // template:begin child
        ticket.AddComment(id: null, "Looking into it.", authorUserId: 7);
        ticket.AddComment(id: null, "Fixed in the next release.", authorUserId: 9);

        // template:end child
        using var provider = BuildModuleProvider();
        var mapper = provider.GetRequiredService<TicketDTOMapper>();
        var projector = provider.GetRequiredService<IEntityDTOProjector<Ticket, TicketDTO, TicketIdentifierType>>();

        var mapped = mapper.MapToDTO(ticket);
        var projected = projector.ProjectTo(new[] { ticket }.AsQueryable()).Single();

        projected.Should().BeEquivalentTo(
            mapped,
            "a projected read and a mapped read are the same endpoint's response; only the registration differs");
    }

    [Fact]
    public void TheProjector_IsDiscoveredByTheModuleScan_WithNoExplicitRegistration()
    {
        // ScanModuleApplicationServices picks up IEntityDTOProjector implementations beside the DTO
        // mappers, so the module's DependencyInjection stays a scan and never names the projector.
        using var provider = BuildModuleProvider();

        provider.GetService<IEntityDTOProjector<Ticket, TicketDTO, TicketIdentifierType>>()
            .Should().BeOfType<TicketDTOProjector>();
    }

    private static ServiceProvider BuildModuleProvider()
    {
        var services = new ServiceCollection();
        services.ScanModuleApplicationServices<ClassReference>();
        return services.BuildServiceProvider();
    }
}
