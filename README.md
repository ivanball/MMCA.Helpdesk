# MMCA.Helpdesk

A minimal, runnable **reference application** built on the [MMCA.Common](../MMCA.Common) framework
(.NET 10, DDD + Clean Architecture + CQRS). It is the worked companion to
[`MMCA.Common/GETTING-STARTED.md`](../MMCA.Common/GETTING-STARTED.md): every step in that guide maps
to real code here. It is built **monolith-first** (the framework's "build the monolith now, extract a
service later" path).

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
`../MMCA.Common/Source`, so it builds against the framework source with no GitHub Packages token. To
consume the published NuGet packages instead, delete `local.props` and add the GitHub feed to
`nuget.config` (see GETTING-STARTED.md, Phase 1).

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
To add real RS256/JWKS auth, add the Identity module (see guide's Phase 8),
set `Authentication:JwtBearer:Authority`, and switch the controller back to `[Authorize]`.

## Status

Build-verified here:

- `dotnet build MMCA.Helpdesk.slnx` -> 0 warnings, 0 errors.
- `dotnet test --solution` -> 33 passing (7 domain + 26 architecture-fitness).
- `dotnet ef migrations add InitialCreate` -> generates `Tickets`, `TicketComments`, and the per-DB
  `OutboxMessages` table with audit, soft-delete, and concurrency columns.

End-to-end run (POST/GET against SQL, and the Phase 8 extraction into a Tickets service behind a
gateway) needs a reachable SQL Server and an Identity issuer, and is described step by step in
[GETTING-STARTED.md](../MMCA.Common/GETTING-STARTED.md). The monolith-to-service change is **host-only**:
the Tickets Domain/Application/Shared/Infrastructure/API code does not change.
