namespace MMCA.Helpdesk.Architecture.Tests;

/// <summary>
/// Declares every framework and module layer assembly for MMCA.Helpdesk. The shared NetArchTest
/// rule library (in MMCA.Common.Testing.Architecture) is parameterized by this map, so the layering,
/// module-isolation, and transport-at-edges rules run identically here (ADR-015).
/// </summary>
internal sealed class HelpdeskArchitectureMap : ArchitectureMapBase
{
    public override string RepoToken => "MMCA.Helpdesk";

    protected override IEnumerable<LayerRef> DefineLayers() =>
    [
        // Framework (MMCA.Common)
        Framework(Layer.Shared, typeof(MMCA.Common.Shared.Abstractions.Result).Assembly),
        Framework(Layer.Domain, typeof(MMCA.Common.Domain.Entities.BaseEntity<>).Assembly),
        Framework(Layer.Application, typeof(MMCA.Common.Application.Services.EntityQueryService<,,>).Assembly),
        Framework(Layer.Infrastructure, typeof(MMCA.Common.Infrastructure.Persistence.DbContexts.ApplicationDbContext).Assembly),
        Framework(Layer.Api, typeof(MMCA.Common.API.Controllers.ApiControllerBase).Assembly),

        // Tickets module
        Module("Tickets", Layer.Domain, typeof(MMCA.Helpdesk.Tickets.Domain.Tickets.Ticket).Assembly),
        Module("Tickets", Layer.Application, typeof(MMCA.Helpdesk.Tickets.Application.ClassReference).Assembly),
        Module("Tickets", Layer.Infrastructure, typeof(MMCA.Helpdesk.Tickets.Infrastructure.AssemblyReference).Assembly),
        Module("Tickets", Layer.Shared, typeof(MMCA.Helpdesk.Tickets.Shared.Tickets.TicketDTO).Assembly),
        Module("Tickets", Layer.Api, typeof(MMCA.Helpdesk.Tickets.API.Controllers.TicketsController).Assembly),
    ];
}
