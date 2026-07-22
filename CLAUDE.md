# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo is

MMCA.Helpdesk is the **runnable reference application** for the [MMCA.Common](../MMCA.Common)
framework: the worked companion to `../MMCA.Common/GETTING-STARTED.md`, where every step in that
guide maps to real code here. It is deliberately **minimal and monolith-first**: one business module
(`Tickets`) exercised end to end through all five layers, built to demonstrate the framework's "build
the monolith now, extract a service later" path.

The workspace-level `../CLAUDE.md` covers the conventions shared across MMCA.Common / Store / ADC /
Helpdesk (.NET 10, `LangVersion: preview`, the five error-severity analyzers + `TreatWarningsAsErrors`,
Result pattern, DDD+CQRS, soft-delete, audit fields, identifier type aliases, Microsoft Testing
Platform + xUnit v3). **Don't re-derive those here**: read that file for the cross-cutting rules. This
file is only the Helpdesk-specific picture.

Reinforced prose rule: never use accents, tildes, or em-dashes, and never use the words "seam" or
"seams" (banned workspace-wide); prefer "boundary", "extension point", "pipeline", or "layer".

## Build, test, run

This scaffold defaults to **local-source mode**: `local.props` (committed in this seed, unlike Store/ADC,
where it is gitignored) sets `UseLocalMMCA=true` and points at `../MMCA.Common/Source`, so the
`MMCA.Common.*` packages resolve via `ProjectReference` to the framework source: no GitHub Packages
token needed. The
`PackageReference`→`ProjectReference` swap lives in `Directory.Build.targets`; `nuget.config` only lists
nuget.org. To consume published packages instead, delete `local.props` and add the GitHub feed (see
GETTING-STARTED.md Phase 1). **Building requires `../MMCA.Common/Source` to exist on disk.**

```bash
dotnet build MMCA.Helpdesk.slnx                       # warning-free under all analyzers
dotnet test  --solution MMCA.Helpdesk.slnx            # 86 tests (14 domain + 72 architecture), NO database needed
dotnet run --project Source/Hosting/MMCA.Helpdesk.AppHost   # interactive terminal ONLY, see caveat below
```

Single test: target the project and pass a Microsoft Testing Platform filter after `--` (NOT VSTest
`--filter`):

```bash
dotnet test --project Tests/Modules/Tickets/MMCA.Helpdesk.Tickets.Domain.Tests/MMCA.Helpdesk.Tickets.Domain.Tests.csproj -- -method "*Create_WithEmptyTitle*"
dotnet test --project Tests/Architecture/MMCA.Helpdesk.Architecture.Tests/MMCA.Helpdesk.Architecture.Tests.csproj -- -class "*ModuleIsolationTests*"
```

EF migration (the design-time factory `DesignTimeSQLServerDbContextFactory` never opens a DB connection
for `add`/`script`; `HELPDESK_TICKETS_SQL` env var overrides the connection string for `apply`):

```bash
dotnet ef migrations add <Name> \
  --project Source/Hosting/MMCA.Helpdesk.Migrations.SqlServer.Tickets \
  --startup-project Source/Hosting/MMCA.Helpdesk.Migrations.SqlServer.Tickets \
  --context SQLServerDbContext
```

**Run caveats (important):**
- The Aspire AppHost **stalls if launched headless**: only run it in an interactive terminal.
- The full POST/GET round-trip needs a **reachable SQL Server**; there is none in the sandbox, so
  integration/E2E behavior can't be run-verified here (matches the workspace `feedback_no_local_sql_for_tests`).
  Unit + architecture tests need no DB and do run.
- The Aspire dashboard exposes three resources: `sql`, `web` (the API), `ui` (Blazor). Open **`ui`** to
  use the app. The `web` API serves `GET/POST /Tickets`, `/health`, `/alive`: **no page at `/`, so the
  API root returns 404 by design.**

## Architecture: how a request flows

The one module, `Tickets`, is split across five projects under `Source/Modules/Tickets/` (`Shared`,
`Domain`, `Application`, `Infrastructure`, `API`): the canonical layer layout. Hosts live separately:
`Source/Hosts/MMCA.Helpdesk.Web` (the API monolith), `Source/Hosts/UI/MMCA.Helpdesk.UI.Web` (Blazor),
`Source/Hosting/` (AppHost + migrations).

**The host DI sequence is load-bearing and fixed** (`Source/Hosts/MMCA.Helpdesk.Web/Program.cs`):
`AddApplication()` → `AddInfrastructure()` → `AddAPI(modulesSettings)` →
`AddErrorResources<TicketsErrorResources>()` (module error-code translations for localized
ProblemDetails, ADR-027) → `ModuleLoader.DiscoverAndRegister(...)`
→ `AddBrokerMessaging()` → **`AddApplicationDecorators()` must come last** (it wraps the
convention-scanned handlers in the FeatureGate→Logging→Caching→Validating→Transactional pipeline; see
ADR-014). `ModuleLoader` discovers `IModule` implementations and registers them in topological order:
`TicketsModule` is a leaf with no dependencies.

**Aggregate conventions** (`Source/Modules/Tickets/.../Domain/Tickets/Ticket.cs`): these are the
patterns the reference app exists to demonstrate, copy them when adding entities:
- Created through a static `Create(...)` **factory returning `Result<Ticket>`** (never a public ctor;
  the framework-wide factory name, enforced by `EntityConventionTests`); invariants live in
  `TicketInvariants` as `Result`-returning methods composed with `Result.Combine`.
- `[IdValueGenerated]` + the `TicketIdentifierType = int` alias → IDs are **database-generated**. The
  factory therefore raises **no "Added" domain event** (the id is still 0); creation is signalled
  *after commit* by `CreateTicketHandler` publishing `TicketOpenedIntegrationEvent` with the real id.
  A `TicketTests` case asserts `DomainEvents` is empty after `Create`; don't "fix" it.
- Mutations (`AddComment`, `UpdateDetails`, `ChangeStatus`, `Delete`) raise a `TicketChanged` **domain
  event** via `AddDomainEvent`, dispatched in-process after `SaveChangesAsync` within the same
  transaction (consumed by `TicketChangedAuditHandler`). `Delete()` cascade-soft-deletes comments.

**Two event paths, deliberately distinct:** *domain events* (`TicketChanged`) are intra-module, dispatched
synchronously post-save; *integration events* (`TicketOpenedIntegrationEvent`) go through the outbox
(`IEventBus.PublishAsync`): in-process in the monolith, over a broker once Tickets is
extracted, with no handler change (ADR-003 / ADR-008). Integration events carry a `SchemaVersion`
(ADR-010).

**Use cases / wiring:** handlers implement `ICommandHandler<,>` / `IQueryHandler<,>` and are
**convention-scanned by Scrutor** via `ScanModuleApplicationServices<ClassReference>()` in the module's
`Application/DependencyInjection.cs` (which uses the C# `extension(IServiceCollection)` syntax): you do
not register each handler by hand. Read endpoints come from `EntityControllerBase`; writes inject
handlers directly into `TicketsController`. Mapping is **manual via Mapperly** source-generators
(`*DTOMapper`, `*RequestMapper`), not AutoMapper (ADR-001). Failures map to RFC 9457 ProblemDetails
through `HandleFailure`.

**Persistence:** `ModuleApplicationDbContext` is abstract and only declares the module's `DbSet`s; the
concrete runtime context is the single `SQLServerDbContext` from MMCA.Common: **one instance per
database, never a per-module context class** (ADR-006). EF entity configurations are auto-discovered by
assembly-name convention, which is why `Infrastructure/DependencyInjection.cs` is a near no-op.

**UI** (`Source/Hosts/UI/...`): Blazor Server + MudBlazor calling the API **server-side** via the typed
`HelpdeskApiClient` + Aspire service discovery (base address `https+http://web` from config): no
browser CORS, no token. On failure it calls `ServiceExceptionHelper.ThrowIfDomainExceptionAsync` to
surface the domain error message before the generic `EnsureSuccessStatusCode` fallback.

## Seed-specific gotchas

- **Issuer-less auth by default.** No Identity module ships in this seed, so `Web/Program.cs` registers
  a bare auth scheme when `Authentication:JwtBearer:Authority` is unset and `TicketsController` is
  `[AllowAnonymous]`. To add real RS256/JWKS auth: add the Identity module (GETTING-STARTED.md Phase 8),
  set the authority, and flip the controller back to `[Authorize]`. Don't add `[Authorize]` without an
  issuer or the pipeline rejects every request.
- **Extraction is host-only.** Turning Tickets into its own service (Phase 8) changes only the
  hosting/AppHost (per-service DB, broker, YARP gateway, JWKS): the
  Domain/Application/Shared/Infrastructure/API code does **not** change. Preserve that property.
- **Architecture fitness functions are real tests.** `Tests/Architecture/` subclasses the shared
  NetArchTest rule bases from `MMCA.Common.Testing.Architecture`, parameterized by
  `HelpdeskArchitectureMap` (which enumerates every framework + module layer assembly, ADR-015). If you
  add a module or layer assembly, register it in that map or the layering/isolation rules silently stop
  covering it.

## Contribution Flow (PR-based)

`main` is server-protected: no direct pushes, and no modifications committed directly on `main`,
documentation-only changes included (this file too). For any modification, branch off an up-to-date
`main` first, commit there, push the branch, open a PR, let the required checks go green (see
`CONTRIBUTING.md`), then squash-merge. Merges here are not deploys.

- **Commit messages use Scoped Commits** (`<scope>: <description>`), not Conventional Commits (see
  `CONTRIBUTING.md`).
- **The one required check is `build-and-test`** (`.github/workflows/ci.yml`): Release build + the
  headless domain/architecture tests. CI checks out `ivanball/MMCA.Common@main` as a sibling and
  builds against its source (local-source mode, no package token), so a change merged to MMCA.Common
  `main` can break Helpdesk CI with no Helpdesk-side change; if CI goes red on an untouched area,
  diff recent Common commits first.
- Helpdesk keeps no `packages.lock.json` files; framework version bumps arrive as `Bump MMCA.Common
  to vX.Y.Z` PRs cut by `/push-release`.
