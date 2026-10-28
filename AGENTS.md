# AGENTS.md

Helpdesk-specific guidance for every coding agent (Claude Code imports this file from `CLAUDE.md`; Copilot, Codex and Cursor read it directly). Cross-repo conventions live once in the workspace `../CLAUDE.md`; framework patterns live in `../MMCA.Common/AGENTS.md` and the ADRs. Do not re-derive either here.

## What this repo is

MMCA.Helpdesk is the **runnable reference application** for [MMCA.Common](../MMCA.Common), and the worked companion to the two adoption guides in `../Website/docs-src/guides/`: `common-GETTING-STARTED.md` (the six-step `dotnet new mmca-app` path) and `common-BUILD-BY-HAND.md` (phase by phase, **every phase mapping to real code here**). It is deliberately minimal and monolith-first: one business module (`Tickets`) exercised end to end through all five layers, demonstrating the "build the monolith now, extract a service later" path.

## This repo is ALSO the dotnet-new template source

The `MMCA.Templates` pack (`mmca-app`, `mmca-module`, `mmca-command`, `mmca-query`) is packed from **this tree**, not a copy. `.template.config/` makes the seed itself a template; `build/templates/stage.ps1` is the only mechanical step, dropping repo-only files and laying `build/templates/overlay/` on top.

- **Never create a parallel copy of the solution for the template.** The single-source property is the whole design: the app whose CI keeps it green IS what adopters get.
- **Renaming or moving anything `stage.ps1` anchors on breaks the pack loudly, by design** (it throws rather than shipping a half-staged template). Update the anchors there.
- **The seed's own green CI does NOT prove the template works.** `build-and-test` builds in local-source mode against Common's `main`; a generated app builds in package mode against a release, and local mode can pass where package-mode Release fails on an analyzer. **Before touching anything under `build/templates/`, `templates/` or `.template.config/`, read `build/templates/MAINTAINING.md` and run `pwsh build/templates/smoke.ps1`.** That file holds the template-engine traps: the `template:begin` marker syntax, the `--database` engine swap, XML double-hyphen comments, the `Comment` rename token, the `Default` data-source rule for generated modules, and the `add-module.ps1` leak check.
- **`comment` is a template rename token** (`--child` replaces the substring everywhere in the seed): write "code notes" or "children" in prose instead.
- **Never rename `.github/workflows/release-templates.yml`**: the `templates-vX.Y.Z` release publishes through keyless OIDC pinned to that filename.

## Build, test, run

This scaffold defaults to **local-source mode**: `local.props` is **committed here** (unlike Store/ADC, where it is gitignored), sets `UseLocalMMCA=true` and points at `../MMCA.Common/Source`, so packages resolve via `ProjectReference` with no token. The swap lives in `Directory.Build.targets`; `nuget.config` lists nuget.org only. To consume published packages instead, just delete `local.props`. **Local-source mode requires `../MMCA.Common/Source` to exist on disk.**

```bash
dotnet build MMCA.Helpdesk.slnx
dotnet test  --solution MMCA.Helpdesk.slnx      # domain + application + architecture, NO database needed
dotnet run --project Source/Hosting/MMCA.Helpdesk.AppHost   # interactive terminal ONLY

dotnet test --project Tests/Modules/Tickets/MMCA.Helpdesk.Tickets.Domain.Tests/MMCA.Helpdesk.Tickets.Domain.Tests.csproj -- --filter-method "*Create_WithEmptyTitle*"

dotnet ef migrations add <Name> \
  --project Source/Hosting/MMCA.Helpdesk.Migrations.SqlServer.Tickets \
  --startup-project Source/Hosting/MMCA.Helpdesk.Migrations.SqlServer.Tickets \
  --context SQLServerDbContext
```

`DesignTimeSQLServerDbContextFactory` never opens a connection for `add`/`script`; `HELPDESK_TICKETS_SQL` overrides the connection string for `apply`.

**Run caveats:**
- The Aspire AppHost **stalls if launched headless**: only run it in an interactive terminal.
- The full POST/GET round-trip needs a reachable SQL Server, which the sandbox has none of. Unit + architecture tests need no DB and do run.
- The dashboard exposes `sql`, `web` (the API) and `ui` (Blazor). Open **`ui`** to use the app. The `web` API serves `GET/POST /Tickets`, `/health`, `/alive`: **no page at `/`, so the API root returns 404 by design.**

## Architecture: how a request flows

The one module, `Tickets`, spans five projects under `Source/Modules/Tickets/` (`Shared`, `Domain`, `Application`, `Infrastructure`, `API`): the canonical layer layout. Hosts live separately: `Source/Hosts/MMCA.Helpdesk.Web` (the API monolith), `Source/Hosts/UI/MMCA.Helpdesk.UI.Web` (Blazor), `Source/Hosting/` (AppHost + migrations). Folder convention follows the workspace rule; **this layout is what `mmca-module` generates, so keep the two in step.**

**The host DI sequence** (`Web/Program.cs`) is composed the way the ADC and Store service hosts compose theirs: `AddInfrastructure()` -> the three opt-ins on top of it (`AddAuditTrail()` -> `AddScheduledJobs()` -> `AddMultiTenancy()`, order irrelevant among themselves) -> `AddAPI(modulesSettings)` -> `AddErrorResources<TicketsErrorResources>()` (module error-code translations for localized ProblemDetails, ADR-027) -> **`AddMmcaApplicationPipeline(pipeline => pipeline.Register(ModuleLoader.DiscoverAndRegister(...)).Register(AddBrokerMessaging(...)))`** -> `AddModuleHealthChecks(moduleLoader)`. **Every handler registration (discovery, broker wiring, any future handler override) goes inside the `AddMmcaApplicationPipeline` call** so the decorators wrap it; non-handler registrations stay outside. `DecoratorPipelineOrderTests` verifies the module's whole handler set.

**Aggregate conventions** (`Domain/Tickets/Ticket.cs`): the patterns the reference app exists to demonstrate.
- Created through a static `Create(...)` **factory returning `Result<Ticket>`**, never a public ctor (enforced by `EntityConventionTests`); invariants live in `TicketInvariants` composed with `Result.Combine`.
- `[IdValueGenerated]` + the `TicketIdentifierType = int` alias means IDs are **database-generated**, so the factory raises **no "Added" domain event** (the id is still 0); creation is signalled *after commit* by `CreateTicketHandler` publishing `TicketOpenedIntegrationEvent` with the real id. A `TicketTests` case asserts `DomainEvents` is empty after `Create`: **do not "fix" it.**
- Mutations raise a `TicketChanged` **domain event**, dispatched in-process after `SaveChangesAsync` within the same transaction. `Delete()` cascade-soft-deletes children.

**The caching pair is wired, not just available:** `GetTicketByIdQuery` implements `IQueryCacheable` and every ticket command implements `ICacheInvalidating`, both keyed through `TicketCacheKeys`. **The decorator matches the two by string prefix, so a read key that drifts out of `TicketCacheKeys.Prefix` goes stale silently**; `TicketCacheInvalidationTests` is what catches that: it discovers every `ICacheInvalidating` command and fails by name when one has no builder, so **a new command needs a builder** (an entry in `HandWrittenBuilders()` or an `ICacheInvalidatingCommandBuilder` class in the test project; `mmca-command` scaffolds one). Keep both halves pointing at `TicketCacheKeys`.

**Two event paths, deliberately distinct:** *domain events* are intra-module, dispatched synchronously post-save; *integration events* go through the outbox (`IEventBus.PublishAsync`): in-process in the monolith, over a broker once Tickets is extracted, with no handler change (ADR-003 / ADR-008). Integration events carry a `SchemaVersion` (ADR-010).

**Use cases / wiring:** handlers are **convention-scanned by Scrutor** via `ScanModuleApplicationServices<ClassReference>()`; you do not register each by hand. The plain-CRUD write side is the framework's own: one `AddEntityCrud<Ticket, ...>()` call closes the generic create/update/delete handlers over the module's types, the mutation stays on the aggregate behind `TicketUpdateApplier`, and the controller constructs `UpdateEntityCommand<...>` directly. **Three ordering facts are load-bearing and silent**: `AddEntityCrud` runs AFTER the scan (it uses `TryAdd`, so the hand-written `CreateTicketHandler` and `TicketUpdateHandler` keep their verbs); the same call registers the validator bridge for the closed framework command (without it `TicketUpdateRequestValidator` silently stops running); and `DeleteTicketHandler` stays hand-written with its own `DeleteTicketCommand`, because it loads the children so `Delete()` can cascade and is the source of the `mmca-command` slice template. `TicketsCrudRegistrationTests` and `DeleteTicketCascadeTests` pin all three. Read endpoints come from `EntityControllerBase`; writes inject handlers into `TicketsController`. Mapping is manual via Mapperly source generators, not AutoMapper (ADR-001).

**Filtered indexes are engine-aware, never a SQL literal.** `TicketConfiguration` uses `HasSoftDeleteFilter(DataSource.SQLServer)` rather than `HasFilter("[IsDeleted] = 0")`. The engine token is the one `engineNameUpper` renames, so a `--database postgresql` scaffold gets `"IsDeleted" = false`; PostgreSQL rejects both the brackets and the comparison with `0` at `CREATE INDEX`. On SQL Server the produced string is byte-identical to the old literal, so the checked-in migrations are unchanged. **Copy this, not a literal, for any new filtered index.**

**Persistence:** `ModuleApplicationDbContext` is abstract and only declares the module's `DbSet`s; the concrete runtime context is the single `SQLServerDbContext` from MMCA.Common: **one instance per database, never a per-module context class** (ADR-006). EF entity configurations are auto-discovered by assembly-name convention, which is why `Infrastructure/DependencyInjection.cs` is a near no-op.

**UI:** Blazor Server + MudBlazor calling the API **server-side** via the typed `HelpdeskApiClient` + Aspire service discovery: no browser CORS, no token. **It throws nothing for a server answer**: every method returns `Result` / `Result<T>` (a ProblemDetails body, a refused connection and a timeout all become failures; a missing ticket is `NotFound`, not null), and only the caller's own `OperationCanceledException` propagates. The pages therefore **branch instead of catching**, using `TryGetValue` and `LocalizedErrorMessage` from `MMCA.Common.UI.Common`.

## Multi-tenancy demo (and the other opt-ins)

This seed is the framework's runnable reference for multi-tenancy, so unlike ADC and Store it turns the feature on. `Ticket` and `TicketComment` implement `ITenantEntity`, which is the whole domain-side change: the framework adds a required `varchar(64)` `TenantId` column plus an index and composes a named `Tenant` query filter with the existing `SoftDelete` one. `Ticket` also implements `IAuditedEntity`; `TicketComment` deliberately does not, because the trail records history per aggregate.

**Two tenants, because there are two isolation modes**: `acme` has no data-source override, so it shares the pooled database and is separated only by the query filter; `globex` overrides the connection string and lands in its own `Helpdesk_Globex` database. The host migrates it at startup in the per-tenant pass, and the outbox drains per `(source, tenant)` pair.

Two details that look wrong until you know them:

- **The override key is `Default`, not `Tickets`.** Per-tenant overrides are keyed by **physical** source name, and the AppHost injects the same connection string as both the `Tickets` logical source and the top-level one, so the resolver collapses `Tickets` onto `Default`. Keying the override `Tickets` fails `ValidateOnStart` with "overrides a SQLServer data source that does not exist". The design-time factory mirrors that collapse for the same reason: `ScheduledJobs` is a Default-only table, so a factory whose physical source is not `Default` scaffolds a migration without it.
- **`RequireTenant` is `false` here, against the framework default of `true`.** This seed runs issuer-less, so no request carries a `tenant_id` claim and fail-closed would answer every unheadered call, including health probes and the guides' first `curl`, with a 400. `false` makes an unresolved caller the **system** caller, which reads across all tenants. **Any app with a real issuer should delete the line and take the fail-closed default.** The trade-off is not free: an unresolved caller can read every tenant's rows, and a **write** with no tenant resolved throws `CrossTenantWriteException` rather than inserting an untenanted row. That is why the Blazor UI stamps the header rather than relying on the permissive default.

Resolution is claim first, then the `X-Tenant-Id` header, so the same `curl` with `acme` and with `globex` hits two different databases through one code path. The UI host sends the header from `Api:TenantId` (default `acme`). **Adopting on existing data**: the migration adds `TenantId` with a `""` default, which matches no tenant, so pre-existing rows become invisible to every tenant while staying visible to the system caller. A real app stamps those rows before turning `RequireTenant` on.

The other two opt-ins are configuration only. `AuditTrail:Enabled` and `Scheduler:Enabled` travel together (`AddAuditTrail` registers the `audit-trail-cleanup` retention job, but only the scheduler runner runs it). Both are mirrored by `EnableAuditTrail` / `EnableScheduler` on the design-time factory, and **those flags must stay in step with the host settings** or the scaffolded model drifts from the running one; `DesignTimeFactoryHostSettingsTests` compares the two for every environment.

## Seed-specific gotchas

- **Issuer-less auth by default.** No Identity module ships here, so `Web/Program.cs` registers a bare auth scheme when `Authentication:JwtBearer:Authority` is unset and `TicketsController` is `[AllowAnonymous]`. To add real RS256/JWKS auth: add the Identity module (BUILD-BY-HAND Phase 8), set the authority, and flip the controller back to `[Authorize]`. **Do not add `[Authorize]` without an issuer** or the pipeline rejects every request.
- **Extraction is host-only.** Turning Tickets into its own service changes only the hosting/AppHost (per-service DB, broker, YARP gateway, JWKS): the Domain/Application/Shared/Infrastructure/API code does **not** change. Preserve that property.
- **Architecture fitness functions are real tests.** `Tests/Architecture/` subclasses the shared NetArchTest bases parameterized by `HelpdeskArchitectureMap` (ADR-015). A new module or layer assembly must be registered in that map or the layering rules silently stop covering it.

## Contribution Flow (PR-based)

`main` is server-protected: branch off an up-to-date `main`, PR, green checks, squash-merge. Documentation-only changes included, this file too. Merges here are not deploys.

- **Commit messages use Scoped Commits** (`<scope>: <description>`), not Conventional Commits.
- **The one required check is `build-and-test`** (Release build + headless domain/architecture tests). Two advisory jobs sit beside it: `template-smoke` (the real gate on template changes) and the PostgreSQL canary (`build/templates/canary-postgresql.ps1`, the only place the SQL the framework emits for PostgreSQL is executed; ADR-113). Without `-ConnectionString` the canary stops after scaffolding, so it runs on a workstation with no server.
- **CI builds against `ivanball/MMCA.Common@main` as a sibling** in local-source mode, so a change merged to Common `main` can break Helpdesk CI with no Helpdesk-side change. If CI goes red on an untouched area, diff recent Common commits first.
- Helpdesk keeps no `packages.lock.json` files; framework bumps arrive as `Bump MMCA.Common to vX.Y.Z` PRs cut by `/push-release`.
