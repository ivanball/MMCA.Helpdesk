using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MMCA.Common.API.Controllers.Administration;
using MMCA.Common.Application.Interfaces;
using MMCA.Common.Shared.Auditing;
using MMCA.Helpdesk.Tickets.API.Controllers;
using MMCA.Helpdesk.Tickets.Domain.Tickets;
using Moq;

namespace MMCA.Helpdesk.Architecture.Tests.Tickets;

/// <summary>
/// Pins the Tickets module's audit-history endpoint (item 12, server side): a one-line controller over
/// the framework's <see cref="AuditHistoryControllerBase"/> that serves the change history of the
/// <see cref="Ticket"/> aggregate at <c>GET Admin/AuditHistory/{entity}/{key}</c>, anonymous in this
/// issuer-less seed for the same reason <see cref="TicketsController"/> is.
/// </summary>
public sealed class AuditHistoryControllerTests
{
    [Fact]
    public void Controller_DerivesFromTheFrameworkBase() =>
        typeof(AuditHistoryController).Should().BeDerivedFrom<AuditHistoryControllerBase>();

    [Fact]
    public void Controller_IsRoutedAtAdminAuditHistory_VersionOne()
    {
        var route = typeof(AuditHistoryController).GetCustomAttribute<RouteAttribute>(inherit: false);
        var apiVersion = typeof(AuditHistoryController).GetCustomAttribute<ApiVersionAttribute>(inherit: false);

        route.Should().NotBeNull("the derived controller supplies the route");
        route.Template.Should().Be("Admin/AuditHistory");
        apiVersion.Should().NotBeNull("the derived controller supplies the API version");
        apiVersion.Versions.Select(version => version.ToString()).Should().ContainSingle().Which.Should().Be("1.0");
    }

    [Fact]
    public void Controller_IsAnonymous_UntilAnIssuerIsAdded() =>
        typeof(AuditHistoryController).IsDefined(typeof(AllowAnonymousAttribute), inherit: false)
            .Should().BeTrue("this seed ships without an Identity issuer, so the inherited audit:view gate would deny every caller");

    [Fact]
    public async Task History_ForTheTicketAggregate_ReadsItsTrail_ByFullTypeName()
    {
        IReadOnlyList<AuditHistoryEntryDTO> entries = [];
        var reader = new Mock<IAuditTrailReader>();
        reader
            .Setup(r => r.GetForEntityAsync(
                typeof(Ticket).FullName!,
                "7",
                1,
                50,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
        var controller = new AuditHistoryController(reader.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var response = await controller.GetHistoryAsync(
            nameof(Ticket).ToUpperInvariant(),
            "7",
            1,
            50,
            TestContext.Current.CancellationToken);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(entries);
        reader.Verify(
            r => r.GetForEntityAsync(typeof(Ticket).FullName!, "7", 1, 50, It.IsAny<CancellationToken>()),
            Times.Once,
            "the route names the aggregate by its simple type name, in any case, and the trail is keyed by its full name");
    }
}
