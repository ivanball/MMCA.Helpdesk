using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using MMCA.Common.Application.Interfaces.Infrastructure.Persistence;
using MMCA.Common.Application.Settings;
using MMCA.Common.Application.UseCases.Contracts;
using MMCA.Common.Application.UseCases.Crud;
using MMCA.Common.Shared.Abstractions;
// template:begin status
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.ChangeStatus;
// template:end status
// template:begin child
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.EditComment;
// template:end child
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;
using Moq;

namespace MMCA.Helpdesk.Tickets.Application.Tests.Concurrency;

/// <summary>
/// ADR-035 round-trip: a write command carries the client's last-seen concurrency token (read from
/// the request's <c>If-Match</c> header), and the handler stamps it back as the tracked entity's
/// ORIGINAL <c>RowVersion</c> before saving. That single call is what turns a concurrent edit into a
/// 412 instead of a silent last-write-wins, and nothing at compile time requires a handler to make
/// it: dropping the line loses the protection with no build error, which is what these tests catch.
/// <para>
/// Handlers are resolved from the module's own convention scan rather than constructed by hand, so
/// the tests exercise the wiring the host actually uses and stay independent of handler
/// constructor shapes.
/// </para>
/// </summary>
public class TicketConcurrencyTokenTests
{
    private const TicketIdentifierType TicketId = 7;

    /// <summary>
    /// The update command is the framework's generic one, carrying the request whole. Built here so
    /// the shape flags reach a single object initializer rather than every call site.
    /// </summary>
    /// <param name="title">The new title.</param>
    /// <param name="rowVersion">The client's last-seen concurrency token.</param>
    /// <returns>An update command for <see cref="TicketId"/>.</returns>
    private static UpdateEntityCommand<Ticket, TicketUpdateRequest, TicketIdentifierType> UpdateCommand(
        string title,
        byte[] rowVersion) =>
        new(
            TicketId,
            new TicketUpdateRequest
            {
                Title = title,
                // template:begin description
                Description = "Returns a 500.",
                // template:end description
            },
            rowVersion);

    [Fact]
    public async Task UpdateDetails_StampsTheClientToken_AsTheOriginalRowVersion()
    {
        byte[] clientToken = [1, 2, 3, 4];
        var ticket = NewTicket();
        var repository = TicketRepositoryReturning(ticket);

        var handler = ResolveHandler<ICommandHandler<UpdateEntityCommand<Ticket, TicketUpdateRequest, TicketIdentifierType>, Result<TicketDTO>>>(repository);
        var result = await handler.HandleAsync(UpdateCommand("Still cannot log in", clientToken));

        result.IsSuccess.Should().BeTrue();
        repository.Verify(r => r.SetOriginalRowVersion(ticket, clientToken), Times.Once);
    }

    // template:begin status
    [Fact]
    public async Task ChangeStatus_StampsTheClientToken_AsTheOriginalRowVersion()
    {
        byte[] clientToken = [5, 6, 7, 8];
        var ticket = NewTicket();
        var repository = TicketRepositoryReturning(ticket);

        var handler = ResolveHandler<ICommandHandler<ChangeTicketStatusCommand, Result<TicketDTO>>>(repository);
        var result = await handler.HandleAsync(
            new ChangeTicketStatusCommand(TicketId, TicketStatus.Closed) { RowVersion = clientToken });

        result.IsSuccess.Should().BeTrue();
        repository.Verify(r => r.SetOriginalRowVersion(ticket, clientToken), Times.Once);
    }

    // template:end status
    // template:begin child
    [Fact]
    public async Task EditComment_StampsTheClientToken_OnTheAggregateRoot()
    {
        byte[] clientToken = [9, 10, 11, 12];
        var ticket = NewTicket();
        var comment = ticket.AddComment(id: null, "Looking into it.", authorUserId: 7).Value!;
        var repository = TicketRepositoryReturning(ticket);

        var handler = ResolveHandler<ICommandHandler<EditCommentCommand, Result>>(repository);
        var result = await handler.HandleAsync(
            new EditCommentCommand(TicketId, comment.Id, "Still looking into it.") { RowVersion = clientToken });

        result.IsSuccess.Should().BeTrue();
        repository.Verify(
            r => r.SetOriginalRowVersion(ticket, clientToken),
            Times.Once,
            "the token a child edit is conditioned on is the owning aggregate's, which is what the ticket read's ETag carries");
    }

    // template:end child
    private static Ticket NewTicket() =>
        Ticket.Create(id: null, "Cannot log in", "The login page returns a 500.", requesterUserId: 42).Value!;

    private static Mock<IRepository<Ticket, TicketIdentifierType>> TicketRepositoryReturning(Ticket ticket)
    {
        var repository = new Mock<IRepository<Ticket, TicketIdentifierType>>();
        repository
            .Setup(r => r.GetByIdAsync(
                TicketId,
                It.IsAny<IEnumerable<string>>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        return repository;
    }

    private static THandler ResolveHandler<THandler>(Mock<IRepository<Ticket, TicketIdentifierType>> repository)
        where THandler : notnull
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.GetRepository<Ticket, TicketIdentifierType>()).Returns(repository.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // The module's own registration rather than the bare scan: the update verb is registered by
        // AddEntityCrud (or, where the aggregate needs an eager load, by the module's subclass that
        // the scan picks up ahead of it), and only AddModuleTicketsApplication runs both in order.
        var services = new ServiceCollection();
        services.AddSingleton(unitOfWork.Object);
        services.AddModuleTicketsApplication(new ApplicationSettings());

        // Disposing the provider is safe here: the resolved handler already holds its dependencies.
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<THandler>();
    }
}
