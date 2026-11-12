using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using MMCA.Common.Application.Interfaces.Infrastructure.Persistence;
using MMCA.Common.Application.Interfaces.Mapping;
using MMCA.Common.Application.Services.Query;
using MMCA.Common.Domain.Interfaces;
using MMCA.Common.Shared.Abstractions;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using MMCA.Helpdesk.Tickets.Shared.Tickets;
using Moq;

namespace MMCA.Helpdesk.Tickets.Application.Tests.Paging;

/// <summary>
/// Pins the module side of cursor paging (items 10 and 47): the ticket list pages through
/// <c>GET Tickets/cursor?sortColumn=createdOn&amp;sortDirection=desc</c>, newest first, so the query
/// service the module registers must accept that sort column and hand the page request to the
/// repository's keyset read. A column the response contract refuses comes back as a validation failure
/// and the repository is never called, which the ticket list would show as an empty, failed page.
/// <para>
/// The service is resolved through the module's real registration over a mocked unit of work, so no
/// database is involved.
/// </para>
/// </summary>
public sealed class TicketCursorPagingTests
{
    // DESIGN-QUESTION: design-api.md item 10 (e) names sortColumn=createdOn for the ticket list, but
    // the query service validates a client sort column against the response contract (TicketDTO), and
    // TicketDTO declares no CreatedOn today. Either the DTO gains it, the module's query service maps it
    // (DTOToEntityPropertyMap), or the client sorts by another column; this test pins the column the
    // design names.
    private const string ListSortColumn = "createdOn";

    [Fact]
    public async Task CursorPage_NewestFirst_ReachesTheRepository()
    {
        var repository = new Mock<IReadRepository<Ticket, TicketIdentifierType>>();
        repository
            .Setup(r => r.GetPageByCursorAsync(
                It.IsAny<KeysetPageRequest>(),
                It.IsAny<ISpecification<Ticket, TicketIdentifierType>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new KeysetCollectionResult<Ticket>([], nextCursor: null)));
        await using var provider = BuildProvider(repository);
        var queryService = provider.GetRequiredService<IEntityQueryService<Ticket, TicketDTO, TicketIdentifierType>>();

        var page = await queryService.GetPageByCursorAsync(
            new KeysetPageRequest(pageSize: 20, sortColumn: ListSortColumn, descending: true, cursor: null),
            cancellationToken: TestContext.Current.CancellationToken);

        page.IsSuccess.Should().BeTrue(
            "the sort column the ticket list sends must be one the query service accepts: {0}",
            string.Join("; ", page.Errors.Select(error => error.Message)));
        repository.Verify(
            r => r.GetPageByCursorAsync(
                It.Is<KeysetPageRequest>(request => request.Descending && request.PageSize == 20),
                It.IsAny<ISpecification<Ticket, TicketIdentifierType>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static ServiceProvider BuildProvider(Mock<IReadRepository<Ticket, TicketIdentifierType>> repository)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.GetReadRepository<Ticket, TicketIdentifierType>()).Returns(repository.Object);

        var services = new ServiceCollection();
        services.AddSingleton(unitOfWork.Object);
        services.AddSingleton(Mock.Of<INavigationMetadataProvider>());
        services.AddSingleton(Mock.Of<IEntityQueryPipeline>());
        services.AddModuleTicketsApplication();
        return services.BuildServiceProvider();
    }
}
