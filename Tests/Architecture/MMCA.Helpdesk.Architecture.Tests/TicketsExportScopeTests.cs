using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MMCA.Common.API.Controllers;
using MMCA.Common.Application.Interfaces.Mapping;
using MMCA.Common.Domain.Interfaces;
using MMCA.Common.Shared.Abstractions;
using MMCA.Helpdesk.Tickets.API.Controllers;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;
using Moq;

namespace MMCA.Helpdesk.Architecture.Tests;

/// <summary>
/// Pins the CSV export decision on <see cref="TicketsController"/>. The framework's export is
/// fail-closed (a null read specification answers 403 <c>Export.RowScopeRequired</c>), and this
/// controller opts in to the whole-table export deliberately: the tickets are the shared queue every
/// caller of the controller already lists, and tenancy is applied by the EF query filter rather than
/// by a specification. Losing the opt-in would turn the export into a 403 for every caller.
/// </summary>
public sealed class TicketsExportScopeTests
{
    [Fact]
    public async Task ExportAsync_OptedInToUnscopedExport_StreamsTheListInsteadOfRefusing()
    {
        var queryService = new Mock<IEntityQueryService<Ticket, TicketDTO, TicketIdentifierType>>();
        queryService
            .Setup(q => q.GetAllAsync(
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<ISpecification<Ticket, TicketIdentifierType>?>(),
                It.IsAny<Dictionary<string, (string Operator, string Value)>?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new PagedCollectionResult<object>([], new PaginationMetadata(0, 1, 1))));

        await using var body = new MemoryStream();
        TicketsController sut = CreateController(queryService.Object, body);

        IActionResult result = await sut.ExportAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeOfType<EmptyResult>(because: "an opted-in export streams the CSV rather than answering 403 Export.RowScopeRequired");
        sut.Response.ContentType.Should().Be(EntityControllerBase<Ticket, TicketDTO, TicketIdentifierType>.CsvContentType);
        body.Length.Should().BePositive(because: "the header row is written even when the queue is empty");
        queryService.Verify(
            q => q.GetAllAsync(
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                null,
                It.IsAny<Dictionary<string, (string Operator, string Value)>?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "the export queries exactly what the unscoped list endpoint does");
    }

    /// <summary>
    /// Builds the controller with a loose mock for every handler. The constructor's handler list
    /// varies with the template's optional module axes, so it is filled by reflection rather than
    /// named argument by argument.
    /// </summary>
    private static TicketsController CreateController(
        IEntityQueryService<Ticket, TicketDTO, TicketIdentifierType> queryService,
        Stream body)
    {
        ConstructorInfo constructor = typeof(TicketsController).GetConstructors().Single();
        object?[] arguments = [.. constructor.GetParameters().Select(p => p.ParameterType.IsInstanceOfType(queryService)
            ? queryService
            : ((Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(p.ParameterType))!).Object)];

        var controller = (TicketsController)constructor.Invoke(arguments);
        var httpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        httpContext.Response.Body = body;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }
}
