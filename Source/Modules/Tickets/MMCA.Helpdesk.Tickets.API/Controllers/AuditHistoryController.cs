using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MMCA.Common.API.Controllers.Administration;
using MMCA.Common.Application.Interfaces;
using MMCA.Helpdesk.Tickets.Domain.Tickets;

namespace MMCA.Helpdesk.Tickets.API.Controllers;

/// <summary>
/// The change history of the <see cref="Ticket"/> aggregate, recorded by the audit trail
/// (<c>AddAuditTrail</c>, ADR-075): <c>GET Admin/AuditHistory/{entity}/{key}?page=&amp;pageSize=</c>
/// answers one page of the recorded changes, newest first. The framework base does the work; this
/// class names the audited types it serves, the route and the API version.
/// <para>
/// One per application, not one per module: a second module adds its own audited aggregates to the
/// type list here rather than mounting a second controller on the same route.
/// </para>
/// </summary>
/// <param name="reader">The audit-trail reader <c>AddAuditTrail</c> registers.</param>
[ApiController]
[Route("Admin/AuditHistory")]
[ApiVersion("1.0")]
// [AllowAnonymous] for the same reason as TicketsController: this seed ships without an Identity
// issuer, so the base's audit:view permission gate would deny every caller. Once you add the Identity
// module (GETTING-STARTED.md Phase 8) and set Authentication:JwtBearer:Authority, delete this attribute
// so the inherited gate applies, and grant audit:view to the roles that may read the history.
[AllowAnonymous]
public sealed class AuditHistoryController(IAuditTrailReader reader)
    : AuditHistoryControllerBase(reader, typeof(Ticket));
