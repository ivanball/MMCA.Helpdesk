using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MMCA.Common.API.Concurrency;
using MMCA.Common.Application.Interfaces.Mapping;
using MMCA.Common.Application.UseCases.Contracts;
using MMCA.Common.Application.UseCases.Crud;
using MMCA.Common.Shared.Abstractions;
using MMCA.Common.Shared.Http;
using MMCA.Helpdesk.Tickets.API.Controllers;
// template:begin child
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.AddComment;
// template:end child
// template:begin status
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.ChangeStatus;
// template:end status
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Create;
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.Delete;
// template:begin child
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.EditComment;
// template:end child
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.GetById;
// template:begin child
using MMCA.Helpdesk.Tickets.Application.Tickets.UseCases.RemoveComment;
// template:end child
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;
using Moq;

namespace MMCA.Helpdesk.Architecture.Tests.Tickets;

/// <summary>
/// Pins the conditional writes of <see cref="TicketsController"/> once they run through the framework's
/// helpers on <c>ApiControllerBase</c> (item 8): the <c>If-Match</c> token the attribute decoded reaches
/// the command, a write that answers with the refreshed ticket also answers with that ticket's
/// <c>ETag</c> (so a client can chain its next conditional write without a second read), and a write
/// that answers 204 carries no tag.
/// <para>
/// The controller is built over mocked handlers and a bare <see cref="DefaultHttpContext"/> holding the
/// decoded token where <see cref="SupportsIfMatchAttribute"/> puts it, so no host, no database and no
/// request pipeline is involved.
/// </para>
/// </summary>
public sealed class TicketsControllerConditionalWriteTests
{
    private const TicketIdentifierType TicketId = 7;

    private static readonly byte[] IfMatchToken = [1, 2, 3, 4, 5, 6, 7, 8];

    private static readonly byte[] RefreshedRowVersion = [8, 7, 6, 5, 4, 3, 2, 1];

    private readonly Mock<ICommandHandler<UpdateEntityCommand<Ticket, TicketUpdateRequest, TicketIdentifierType>, Result<TicketDTO>>> _update = new();

    // template:begin status
    private readonly Mock<ICommandHandler<ChangeTicketStatusCommand, Result<TicketDTO>>> _changeStatus = new();

    // template:end status
    // template:begin child
    private readonly Mock<ICommandHandler<EditCommentCommand, Result>> _editChild = new();

    // template:end child
    [Fact]
    public async Task UpdateAsync_PassesTheIfMatchTokenToTheCommand()
    {
        var refreshed = NewTicketDTO(RefreshedRowVersion);
        _update
            .Setup(h => h.HandleAsync(
                It.IsAny<UpdateEntityCommand<Ticket, TicketUpdateRequest, TicketIdentifierType>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(refreshed));
        var controller = CreateController();

        await controller.UpdateAsync(TicketId, NewUpdateRequest(), TestContext.Current.CancellationToken);

        _update.Verify(
            h => h.HandleAsync(
                It.Is<UpdateEntityCommand<Ticket, TicketUpdateRequest, TicketIdentifierType>>(c =>
                    c.Id == TicketId && c.RowVersion.SequenceEqual(IfMatchToken)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Success_Returns200WithTheTicket_AndItsETag()
    {
        var refreshed = NewTicketDTO(RefreshedRowVersion);
        _update
            .Setup(h => h.HandleAsync(
                It.IsAny<UpdateEntityCommand<Ticket, TicketUpdateRequest, TicketIdentifierType>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(refreshed));
        var controller = CreateController();

        var response = await controller.UpdateAsync(TicketId, NewUpdateRequest(), TestContext.Current.CancellationToken);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(refreshed);
        controller.Response.Headers[ConcurrencyETag.ETagHeaderName].ToString().Should().Be(
            ConcurrencyETag.Format(RefreshedRowVersion),
            "the refreshed ticket's token is the precondition of the caller's next write");
    }

    // template:begin status
    [Fact]
    public async Task ChangeStatusAsync_PassesTheIfMatchTokenToTheCommand()
    {
        var refreshed = NewTicketDTO(RefreshedRowVersion);
        _changeStatus
            .Setup(h => h.HandleAsync(It.IsAny<ChangeTicketStatusCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(refreshed));
        var controller = CreateController();

        await controller.ChangeStatusAsync(
            TicketId,
            new ChangeTicketStatusRequest(TicketStatus.Resolved),
            TestContext.Current.CancellationToken);

        _changeStatus.Verify(
            h => h.HandleAsync(
                It.Is<ChangeTicketStatusCommand>(c =>
                    c.TicketId == TicketId && c.Status == TicketStatus.Resolved && c.RowVersion.SequenceEqual(IfMatchToken)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ChangeStatusAsync_Success_Returns200WithTheTicket_AndItsETag()
    {
        var refreshed = NewTicketDTO(RefreshedRowVersion);
        _changeStatus
            .Setup(h => h.HandleAsync(It.IsAny<ChangeTicketStatusCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(refreshed));
        var controller = CreateController();

        var response = await controller.ChangeStatusAsync(
            TicketId,
            new ChangeTicketStatusRequest(TicketStatus.Resolved),
            TestContext.Current.CancellationToken);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(refreshed);
        controller.Response.Headers[ConcurrencyETag.ETagHeaderName].ToString().Should().Be(
            ConcurrencyETag.Format(RefreshedRowVersion),
            "the refreshed ticket's token is the precondition of the caller's next write");
    }

    // template:end status
    // template:begin child
    [Fact]
    public async Task EditCommentAsync_Success_Returns204_WithoutETag_AndPassesTheToken()
    {
        _editChild
            .Setup(h => h.HandleAsync(It.IsAny<EditCommentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var controller = CreateController();

        var response = await controller.EditCommentAsync(
            TicketId,
            default,
            new EditCommentRequest("Rebooted, still offline."),
            TestContext.Current.CancellationToken);

        response.Should().BeOfType<NoContentResult>();
        controller.Response.Headers.ContainsKey(ConcurrencyETag.ETagHeaderName).Should().BeFalse(
            "a 204 carries no body, so there is no refreshed row to tag");
        _editChild.Verify(
            h => h.HandleAsync(
                It.Is<EditCommentCommand>(c => c.TicketId == TicketId && c.RowVersion.SequenceEqual(IfMatchToken)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // template:end child
    [Fact]
    public async Task UpdateAsync_WithoutADecodedToken_Throws_AndTheHandlerNeverRuns()
    {
        var controller = CreateController();
        controller.ControllerContext.HttpContext.Items.Remove(SupportsIfMatchAttribute.TokenItemKey);

        var act = async () => await controller.UpdateAsync(TicketId, NewUpdateRequest(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>(
            "only an action carrying [SupportsIfMatch] has a decoded token to state");
        _update.Verify(
            h => h.HandleAsync(
                It.IsAny<UpdateEntityCommand<Ticket, TicketUpdateRequest, TicketIdentifierType>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// The controller over the mocked write handlers, with the If-Match token already decoded into the
    /// request items, as <see cref="SupportsIfMatchAttribute"/> leaves it before the action runs.
    /// </summary>
    /// <returns>The controller under test.</returns>
    private TicketsController CreateController()
    {
        var controller = new TicketsController(
            queryService: Mock.Of<IEntityQueryService<Ticket, TicketDTO, TicketIdentifierType>>(),
            getByIdHandler: Mock.Of<IQueryHandler<GetTicketByIdQuery, Result<TicketDTO>>>(),
            createHandler: Mock.Of<ICommandHandler<TicketCreateRequest, Result<TicketDTO>>>(),
            updateHandler: _update.Object,
            // template:begin status
            changeStatusHandler: _changeStatus.Object,
            // template:end status
            deleteHandler: Mock.Of<ICommandHandler<DeleteTicketCommand, Result>>(),
            // template:begin child
            addCommentHandler: Mock.Of<ICommandHandler<AddCommentCommand, Result<TicketCommentDTO>>>(),
            editCommentHandler: _editChild.Object,
            removeCommentHandler: Mock.Of<ICommandHandler<RemoveCommentCommand, Result>>(),
            // template:end child
            logger: NullLogger<TicketsController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Items[SupportsIfMatchAttribute.TokenItemKey] = IfMatchToken;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static TicketUpdateRequest NewUpdateRequest() => new()
    {
        Title = "Printer offline",
        // template:begin description
        Description = "The third-floor printer drops off the network.",
        // template:end description
    };

    private static TicketDTO NewTicketDTO(byte[] rowVersion) => new()
    {
        Id = TicketId,
        RowVersion = rowVersion,
        Title = "Printer offline",
        // template:begin description
        Description = "The third-floor printer drops off the network.",
        // template:end description
        // template:begin status
        Status = TicketStatus.Open,
        // template:end status
        // template:begin owner
        RequesterUserId = 42,
        // template:end owner
    };
}
