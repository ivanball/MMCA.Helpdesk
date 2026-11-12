using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MMCA.Common.API.Authorization;
using MMCA.Common.API.Controllers.Webhooks;
using MMCA.Common.Shared.Auth.Permissions;
using MMCA.Helpdesk.Tickets.API.Controllers;

namespace MMCA.Helpdesk.Architecture.Tests.Tickets;

/// <summary>
/// Pins the outbound-webhook admin endpoint of the worked example (item 35): a controller over the
/// framework's <see cref="WebhookSubscriptionsControllerBase"/> with route and API version only, that
/// re-declares the <c>webhooks:manage</c> permission on itself. The re-declaration is what the
/// anonymous-endpoint gate reads (it inspects declared attributes only), and in this issuer-less seed
/// it denies everyone, the safe direction, until an Identity issuer is added.
/// </summary>
public sealed class WebhookSubscriptionsControllerTests
{
    [Fact]
    public void Controller_DerivesFromTheFrameworkBase() =>
        typeof(WebhookSubscriptionsController).Should().BeDerivedFrom<WebhookSubscriptionsControllerBase>();

    [Fact]
    public void Controller_IsRoutedAtAdminWebhookSubscriptions_VersionOne()
    {
        var route = typeof(WebhookSubscriptionsController).GetCustomAttribute<RouteAttribute>(inherit: false);
        var apiVersion = typeof(WebhookSubscriptionsController).GetCustomAttribute<ApiVersionAttribute>(inherit: false);

        route.Should().NotBeNull("the derived controller supplies the route");
        route!.Template.Should().Be("Admin/WebhookSubscriptions");
        apiVersion.Should().NotBeNull("the derived controller supplies the API version");
        apiVersion!.Versions.Select(version => version.ToString()).Should().ContainSingle().Which.Should().Be("1.0");
    }

    [Fact]
    public void Controller_RedeclaresManageWebhooks_AndIsNotAnonymous()
    {
        typeof(WebhookSubscriptionsController).GetCustomAttributes<HasPermissionAttribute>(inherit: false)
            .Select(attribute => attribute.Permission)
            .Should().ContainSingle()
            .Which.Should().Be(AdministrationPermissions.ManageWebhooks);
        typeof(WebhookSubscriptionsController).IsDefined(typeof(AllowAnonymousAttribute), inherit: false)
            .Should().BeFalse("the subscription admin hands out signing secrets, so it is never anonymous");
    }
}
