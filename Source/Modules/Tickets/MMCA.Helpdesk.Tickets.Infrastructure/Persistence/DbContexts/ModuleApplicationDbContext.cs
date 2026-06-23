using Microsoft.EntityFrameworkCore;
using MMCA.Common.Application.Interfaces.Infrastructure;
using MMCA.Common.Infrastructure.Persistence.DataSources;
using MMCA.Common.Infrastructure.Persistence.DbContexts;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Tickets.Infrastructure.Persistence.DbContexts;

/// <summary>
/// Tickets module's abstract DbContext. Declares the module's aggregate-root <see cref="DbSet{TEntity}"/>
/// properties. The concrete runtime context is the single <c>SQLServerDbContext</c> from MMCA.Common
/// (one instance per database, ADR-006); this type documents the module's entity set.
/// </summary>
public abstract class ModuleApplicationDbContext(
    DbContextOptions options,
    IServiceProvider serviceProvider,
    IEntityConfigurationAssemblyProvider assemblyProvider,
    PhysicalDataSource physicalDataSource)
    : ApplicationDbContext(options, serviceProvider, assemblyProvider, physicalDataSource)
{
    internal DbSet<Ticket> Tickets { get; set; }
    internal DbSet<TicketComment> TicketComments { get; set; }
}
