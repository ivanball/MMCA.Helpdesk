using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using MMCA.Common.API.Authorization;
using MMCA.Common.API.Controllers.Webhooks;
using MMCA.Common.Shared.Auth.Permissions;

namespace MMCA.Helpdesk.Tickets.API.Controllers;

/// <summary>
/// The outbound webhook subscription admin endpoints (<c>Admin/WebhookSubscriptions</c>): list,
/// create (the signing secret is shown once), update, rotate the secret, disable, enable and delete.
/// The framework base does the work; this class supplies the route and the API version. Every action
/// answers 404 while <c>Webhooks:Enabled</c> is off.
/// <para>
/// One per application, not one per module: every module's webhook events share the one
/// subscription table, so a second module adds no second controller.
/// </para>
/// </summary>
[ApiController]
[Route("Admin/WebhookSubscriptions")]
[ApiVersion("1.0")]
// Re-declared from the base so the explicit-authorization fitness test sees a decision on this type.
// Never anonymous: the responses carry signing secrets. In this issuer-less seed the gate denies every
// caller, the safe direction; it opens once the Identity module is added and webhooks:manage granted.
[HasPermission(AdministrationPermissions.ManageWebhooks)]
public sealed class WebhookSubscriptionsController : WebhookSubscriptionsControllerBase;
