# MMCA.Helpdesk

A minimal, runnable **reference application** built on the
[MMCA.Common](https://github.com/ivanball/MMCA.Common) framework (.NET 10, DDD + Clean Architecture +
CQRS). It is the worked companion to the two adoption paths: the six-step
[Getting Started](https://ivanball.github.io/docs/guides/common-GETTING-STARTED.html), which scaffolds
a solution from this tree with `dotnet new mmca-app`, and
[Building by Hand](https://ivanball.github.io/docs/guides/common-BUILD-BY-HAND.html), whose every phase
maps to real code here. It is built **monolith-first** (the framework's "build the monolith now,
extract a service later" path).

**New here? Start with this repository.** It is the fastest way to see the framework work end to end:
one module, five layers, one command to run.

**Starting your own app?** Do not copy this repo by hand. It is also the source of the
`MMCA.Templates` `dotnet new` pack, so you can scaffold the same structure under your own names:

```bash
dotnet new install MMCA.Templates
dotnet new mmca-app -n Contoso.Support --module Orders --aggregate Order
```

That is a green build, passing architecture-fitness tests, and a migration-ready solution in
seconds. [Getting Started](https://ivanball.github.io/docs/guides/common-GETTING-STARTED.html) walks
that from nothing to a running app in six steps;
[Templates](https://ivanball.github.io/docs/guides/common-TEMPLATES.html) documents all four
templates and every parameter; and `build/templates/README.md` covers how the pack is built from
this tree.

- The framework: <https://github.com/ivanball/MMCA.Common> (`dotnet add package MMCA.Common.API`)
- Full documentation, ADRs, and scorecards: <https://ivanball.github.io/docs/>
- Long-form articles on the patterns used here: <https://ivanball.github.io/writing.html>

The domain is a small support-ticket app: a `Ticket` aggregate with `TicketComment` children, opened
through a factory that returns `Result<T>`, mutated through guarded methods that raise domain events,
persisted with soft-delete + audit fields, and exposed through a REST controller.

## Layout

```
Source/
  Modules/Tickets/                         the one business module, five layers
    MMCA.Helpdesk.Tickets.Shared           DTOs, requests, identifier aliases, integration event
    MMCA.Helpdesk.Tickets.Domain           Ticket aggregate, TicketComment, invariants, domain events
    MMCA.Helpdesk.Tickets.Application       create/add-comment use cases, validators, Mapperly mappers, DI
    MMCA.Helpdesk.Tickets.Infrastructure    EF Core entity configurations, DI
    MMCA.Helpdesk.Tickets.API               TicketsController, the IModule, DI
  Hosts/MMCA.Helpdesk.Web                   the monolith API host (the fixed DI sequence + ModuleLoader)
  Hosts/UI/MMCA.Helpdesk.UI.Web             Blazor Server + MudBlazor front end (calls the API server-side)
  Hosting/MMCA.Helpdesk.AppHost             Aspire orchestrator (SQL + the API host + the UI)
  Hosting/MMCA.Helpdesk.Migrations.SqlServer.Tickets   per-DB EF migrations + design-time factory
Tests/
  Modules/Tickets/...Domain.Tests           xUnit v3 domain tests
  Architecture/...Architecture.Tests        IArchitectureMap + the shared fitness-function rules
```

## Build, test, run

This scaffold defaults to **local-source mode**: `local.props` sets `UseLocalMMCA=true` and points at
`../MMCA.Common/Source`, so it builds against the framework source when you have both repositories
checked out side by side. To consume the published packages instead, delete `local.props`: the
`MMCA.Common.*` packages are on nuget.org, so no extra feed and no token are needed
(see [Building by Hand](https://ivanball.github.io/docs/guides/common-BUILD-BY-HAND.html), Phase 1).

```bash
# Build everything (warning-free under the five analyzers + TreatWarningsAsErrors)
dotnet build MMCA.Helpdesk.slnx

# Run the unit + architecture tests (no database needed)
dotnet test --solution MMCA.Helpdesk.slnx

# Add / update the EF migration (the design-time factory never opens a DB connection)
dotnet ef migrations add <Name> \
  --project Source/Hosting/MMCA.Helpdesk.Migrations.SqlServer.Tickets \
  --startup-project Source/Hosting/MMCA.Helpdesk.Migrations.SqlServer.Tickets \
  --context SQLServerDbContext

# Run the app (interactive terminal only: the Aspire AppHost stalls if launched headless)
dotnet run --project Source/Hosting/MMCA.Helpdesk.AppHost
```

When running, the Aspire dashboard lists three resources: `sql`, `web` (the API), and `ui` (the Blazor
front end). Open the **`ui`** endpoint to browse tickets and open new ones in the browser; the UI is a
**Blazor Server + MudBlazor** app that calls the API server-side via Aspire service discovery (no CORS,
no token needed). The `web` API itself exposes `GET/POST /Tickets`, `/health`, and `/alive` (there is
no page at `/` on the API host, so opening the API root returns 404 by design).

This seed ships **without an Identity issuer**, so it runs issuer-less: the API host registers a bare
auth scheme and `TicketsController` is `[AllowAnonymous]`, so the endpoints are reachable with no token.
To add real RS256/JWKS auth, add the Identity module (see
[Building by Hand](https://ivanball.github.io/docs/guides/common-BUILD-BY-HAND.html), Phase 8),
set `Authentication:JwtBearer:Authority`, and switch the controller back to `[Authorize]`.

## Multi-tenancy demo

This seed is also the framework's runnable **multi-tenancy** reference, so it ships the feature turned
on (the production apps deliberately do not). `Ticket` and `TicketComment` implement `ITenantEntity`,
which is the entire domain-side change: the framework adds the `TenantId` column and composes a tenant
query filter with the existing soft-delete one. Two tenants are configured because there are two
isolation modes to show:

| Tenant | Isolation | Where its rows live |
|---|---|---|
| `acme` | shared schema, tenant query filter | the pooled `Helpdesk` database |
| `globex` | database per tenant | its own `Helpdesk_Globex` database, via a per-tenant `DataSources` override the AppHost injects |

The tenant is resolved from a `tenant_id` claim first, then the `X-Tenant-Id` header, so the same call
against two headers reaches two different databases:

```bash
curl -H "X-Tenant-Id: acme"   https://localhost:<port>/Tickets
curl -H "X-Tenant-Id: globex" https://localhost:<port>/Tickets
```

The Blazor UI sends the header on every server-side call from `Api:TenantId` (default `acme`), so
switching that one setting re-points the whole front end at the other tenant. Because this seed runs
issuer-less, `Tenancy:RequireTenant` is `false` here rather than the framework's fail-closed default: a
caller with no tenant is the **system** caller and reads across tenants. An app with a real issuer
should drop that line and keep the default. Alongside tenancy, the seed turns on the framework's
**audit trail** (field-level history for `Ticket`) and the **job scheduler** that runs its retention
job, and every entity controller now also serves `GET /Tickets/export` as RFC 4180 CSV.

## Status

Build-verified here:

- `dotnet build MMCA.Helpdesk.slnx` -> 0 warnings, 0 errors.
- `dotnet test --solution` -> 91 passing (domain + application + architecture-fitness), no database needed.
- `dotnet ef migrations add InitialCreate` -> generates `Tickets`, `TicketComments`, and the per-DB
  `OutboxMessages` table with audit, soft-delete, and concurrency columns. The follow-up migration adds
  the `TenantId` columns plus the framework's `AuditTrailEntries` and `ScheduledJobs` tables.

End-to-end run (POST/GET against SQL, and the Phase 8 extraction into a Tickets service behind a
gateway) needs a reachable SQL Server and an Identity issuer, and is described step by step in
[Building by Hand](https://ivanball.github.io/docs/guides/common-BUILD-BY-HAND.html). The
monolith-to-service change is **host-only**:
the Tickets Domain/Application/Shared/Infrastructure/API code does not change.
