using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using MMCA.Common.Application.Interfaces.Infrastructure.Persistence;
using MMCA.Common.Application.Settings;
using MMCA.Common.Application.UseCases.Contracts;
using MMCA.Common.Shared.Abstractions;
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Create;
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Delete;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using Moq;

namespace MMCA.Helpdesk.Tickets.Application.Tests.Deletion;

/// <summary>
/// Pins why <see cref="DeleteTicketHandler"/> stays hand-written instead of handing the delete verb
/// to the framework's generic handler: the soft delete has to reach the ticket's children, and
/// <c>Delete()</c> can only cascade over children that were loaded. A handler that fetched the ticket
/// alone, or untracked, would mark nothing beneath it (or nothing at all) and still answer success,
/// so nothing but these tests notices.
/// <para>
/// The handler is resolved from the module's own registration, which is the same resolution the
/// controller's injected delete handler goes through.
/// </para>
/// </summary>
public sealed class DeleteTicketCascadeTests
{
    private const TicketIdentifierType TicketId = 7;

    [Fact]
    public void DeleteCommand_ResolvesTo_TheHandWrittenHandler()
    {
        var handler = ResolveHandler(new Mock<IRepository<Ticket, TicketIdentifierType>>(), new Mock<IUnitOfWork>());

        handler.Should().BeOfType<DeleteTicketHandler>(
            "the generic delete handler AddEntityCrud also registers loads no children, so the cascade would silently stop");
    }

    [Fact]
    public async Task Delete_LoadsTheTicketTracked_AndSoftDeletesIt()
    {
        var ticket = await NewTicketAsync();
        var load = new LoadRecorder();
        var repository = TicketRepositoryReturning(ticket, load);
        var unitOfWork = UnitOfWorkOver(repository);

        var result = await ResolveHandler(repository, unitOfWork).HandleAsync(
            new DeleteTicketCommand(TicketId),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        load.AsTracking.Should().BeTrue("a soft delete on an untracked entity is never written by SaveChanges");
        ticket.IsDeleted.Should().BeTrue();
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_OfAMissingTicket_ReportsNotFound_AndSavesNothing()
    {
        var repository = new Mock<IRepository<Ticket, TicketIdentifierType>>();
        var unitOfWork = UnitOfWorkOver(repository);

        var result = await ResolveHandler(repository, unitOfWork).HandleAsync(
            new DeleteTicketCommand(TicketId),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // template:begin child
    [Fact]
    public async Task Delete_EagerLoadsTheChildren_SoTheCascadeReachesThem()
    {
        var ticket = await NewTicketAsync();
        var child = ticket.AddComment(id: null, "Looking into it.", authorUserId: 7).Value!;
        var load = new LoadRecorder();
        var repository = TicketRepositoryReturning(ticket, load);

        var result = await ResolveHandler(repository, UnitOfWorkOver(repository)).HandleAsync(
            new DeleteTicketCommand(TicketId),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        load.Includes.Should().Contain(
            nameof(Ticket.Comments),
            "without the eager load the children are never in memory, and Delete() cascades over an empty collection");
        child.IsDeleted.Should().BeTrue("the aggregate's Delete() soft-deletes every loaded child with it");
    }

    // template:end child
    /// <summary>
    /// Builds the ticket through the module's own create mapper rather than the domain factory, so
    /// the shape flags reach a single object initializer instead of a positional argument list.
    /// </summary>
    /// <returns>A new, open ticket.</returns>
    private static async Task<Ticket> NewTicketAsync()
    {
        var created = await new TicketCreateRequestMapper().CreateEntityAsync(
            new TicketCreateRequest
            {
                Title = "Cannot log in",
                // template:begin description
                Description = "The login page returns a 500.",
                // template:end description
                // template:begin owner
                RequesterUserId = 42,
                // template:end owner
            },
            TestContext.Current.CancellationToken);

        return created.Value!;
    }

    private static Mock<IRepository<Ticket, TicketIdentifierType>> TicketRepositoryReturning(Ticket ticket, LoadRecorder load)
    {
        var repository = new Mock<IRepository<Ticket, TicketIdentifierType>>();
        repository
            .Setup(r => r.GetByIdAsync(
                TicketId,
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Callback<TicketIdentifierType, IEnumerable<string>, bool, CancellationToken>((_, includes, asTracking, _) =>
            {
                load.Includes = [.. includes];
                load.AsTracking = asTracking;
            })
            .ReturnsAsync(ticket);
        return repository;
    }

    private static Mock<IUnitOfWork> UnitOfWorkOver(Mock<IRepository<Ticket, TicketIdentifierType>> repository)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.GetRepository<Ticket, TicketIdentifierType>()).Returns(repository.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return unitOfWork;
    }

    private static ICommandHandler<DeleteTicketCommand, Result> ResolveHandler(
        Mock<IRepository<Ticket, TicketIdentifierType>> repository,
        Mock<IUnitOfWork> unitOfWork)
    {
        unitOfWork.Setup(u => u.GetRepository<Ticket, TicketIdentifierType>()).Returns(repository.Object);

        var services = new ServiceCollection();
        services.AddSingleton(unitOfWork.Object);
        services.AddModuleTicketsApplication(new ApplicationSettings());

        // Disposing the provider is safe here: the resolved handler already holds its dependencies.
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<ICommandHandler<DeleteTicketCommand, Result>>();
    }

    /// <summary>What the handler asked the repository for, read back by the assertions.</summary>
    private sealed class LoadRecorder
    {
        public IReadOnlyList<string> Includes { get; set; } = [];

        public bool AsTracking { get; set; }
    }
}
