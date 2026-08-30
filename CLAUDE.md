# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo is

MMCA.Helpdesk is the **runnable reference application** for the [MMCA.Common](../MMCA.Common)
framework: the worked companion to the two adoption guides, both canonical in the Website repo under
`../Website/docs-src/guides/`. `common-GETTING-STARTED.md`
(<https://ivanball.github.io/docs/guides/common-GETTING-STARTED.html>) is the six-step path that
scaffolds a solution from this tree with `dotnet new mmca-app`; `common-BUILD-BY-HAND.md`
(<https://ivanball.github.io/docs/guides/common-BUILD-BY-HAND.html>) is the phase-by-phase
walkthrough, and **every one of its phases maps to real code here**. It is deliberately **minimal and
monolith-first**: one business module
(`Tickets`) exercised end to end through all five layers, built to demonstrate the framework's "build
the monolith now, extract a service later" path.

The workspace-level `../CLAUDE.md` covers the conventions shared across MMCA.Common / Store / ADC /
Helpdesk (.NET 10, `LangVersion: preview`, the five error-severity analyzers + `TreatWarningsAsErrors`,
Result pattern, DDD+CQRS, soft-delete, audit fields, identifier type aliases, Microsoft Testing
Platform + xUnit v3). **Don't re-derive those here**: read that file for the cross-cutting rules. This
file is only the Helpdesk-specific picture.

Reinforced prose rule: never use accents, tildes, or em-dashes, and never use the words "seam" or
"seams" (banned workspace-wide); prefer "boundary", "extension point", "pipeline", or "layer".

## This repo is ALSO the dotnet-new template source

The `MMCA.Templates` pack (`mmca-app`, `mmca-module`, `mmca-command`, `mmca-query`) is packed from
**this tree**, not from a copy. `.template.config/` at the root makes the seed itself a template;
`build/templates/stage.ps1` is the only mechanical step, dropping the files that belong to this repo
rather than to a generated app and laying `build/templates/overlay/` on top. Consequences:

- **Never create a parallel copy of the solution for the template.** The single-source property is
  the whole design: the app whose CI keeps it green IS what adopters get.
- **Renaming or moving anything the staging script anchors on breaks the pack loudly**, by design:
  `stage.ps1` throws rather than shipping a half-staged template. Update the anchors there.
- **The seed's own green CI does NOT prove the template works.** `build-and-test` builds in
  local-source mode against MMCA.Common's `main`; a generated app builds in package mode against a
  released version, and local mode can pass where package-mode Release fails on an analyzer
  (workspace memory `feedback_localprops_masks_ci_analyzers`). The `template-smoke` job is the real
  gate: run `pwsh build/templates/smoke.ps1` before touching anything under `build/templates/`,
  `templates/`, or `.template.config/`. It runs three full generate + restore + Release build + test
  cases: two module shapes (all axes off with a `--title` rename; fully default) and one SOLUTION
  shape (`--database sqlite --no-aspire`). Two of the three go on to add a second module through
  `build/add-module.ps1`, for different reasons: the first proves the WIRE-UPS (every edit, the child
  rename, the module-only axes, the slices, the refused rerun), and the sqlite case proves the ENGINE
  reaches the second module (thin on purpose: no slices, no child rename). Only the first scaffolds
  slices.
- **One thing the scaffold deliberately does not hand over**: using-directive and alias order, because
  a rename invalidates it and no fixed order is right for every generated name (SA1210 and SA1211 are
  relaxed in the staged `.editorconfig` only, along with IDE0021 for the shape flags). It is documented
  in the generated README. There is deliberately **no `dotnet format` post-action**: dotnet new's
  run-script post action (`3A7C4B45-1F5D-4A30-959A-51B88E82B5D2`) needs `--allow-scripts yes` and
  otherwise PROMPTS, which would hang `smoke.ps1` and every adopter's own automation, and it would
  also run before the app has been built. The `.editorconfig` delta plus the README command is the
  supported answer.
- **The `IntegrationEventContractTests` subclass DOES ship**, and used to not. `IntegrationEventContractTestsBase`
  compares each event's members as an unordered set, so the aggregate's own Id moving position is no
  longer a difference, and everything else in the literal is an ordinary symbol substitution. The one
  member a flag can remove (`RequesterUserId`) is handled by `$optionalAxisLines` like any other
  comma-separated list. `build/add-module.ps1` appends the new module's event to `ExpectedContract`
  as its 8th wire-up, so a second module does not turn the adopter's next test run red.
- **The optional axes live in the seed as ordinary comments.** `--flat` (no child collection),
  `--no-status` (no status axis), `--no-description` (no long-text property), `--no-owner` (no
  owning-user property), and `--child <Name>` (rename the child concept) are shaped two
  ways: whole files come out through `sources.modifiers` conditions in each `template.json`, and
  partial-file regions are marked in the seed with `// template:begin child` / `// template:end child`
  (`<!-- ... -->` in XML and resx, `@* ... *@` in razor, `/// ...` inside an XML doc block). Axis
  labels are `child` -> `!flat`, `status` -> `!noStatus`, `description` -> `!noDescription`,
  `owner` -> `!noOwner`, plus four combination labels: `childStatus` -> `!(flat || noStatus)`,
  `statusOwner` -> `!(noStatus || noOwner)`, `statusOrOwner` -> `!(noStatus && noOwner)`,
  `childOrOwner` -> `!(flat && noOwner)`.
  `stage.ps1` converts them to dotnet-new directives in the STAGED copies and throws on an
  unbalanced or unknown-label marker. **Never put a raw `//#if` in the seed**, and keep a region's
  trailing blank line INSIDE it (a marker followed by a blank line is SA1512).
- **Two SOLUTION axes sit beside the four module ones**, and they behave differently. `--database
  sqlserver|sqlite` and `--no-aspire` use the same marker mechanism (labels `sqlserver`, `sqlite`,
  `aspire`, `aspireSqlServer`, the last being `!(noAspire || useSqlite)` so one line can carry both),
  plus three things the module axes never needed:
  - **`--database` is a SWAP, not a removal**, and the seed can only hold one branch of a swap. So the
    seed keeps the SQL Server code inside a `sqlserver` region, and `stage.ps1`'s `$engineAlternatives`
    table **injects** the SQLite branch as a sibling `sqlite` region at staging time (three entries
    today: the AppHost block, the health-check call, the design-time connection string). Nothing else
    is engine-specific in code, because two derived symbols do the rest by substring: `engineName`
    rewrites `SqlServer` (both provider package ids, the migrations project's folder / namespace /
    assembly, and `CreateSqlServer`) and `engineNameUpper` rewrites `SQLServer` (the design-time
    factory's file and class, `SQLServerDbContext`, `EntityTypeConfigurationSQLServer`, and the
    `SQLServerConnectionString` / `SQLServerMigrationsAssembly` settings).
  - **`.slnx` carries markers** (added to `$markerStyles` with the XML comment form), which is how
    `--no-aspire` drops the AppHost's `<Project>` line. Verified: the solution parsers keep comments.
  - **Three whole files are variants, not regions**, because `.json` and `.md` differ structurally
    rather than by a line: the API host's `appsettings.sqlite.json`, the UI host's
    `appsettings.standalone.json`, and `README.standalone.md`, all shipped by the overlay and renamed
    into place by `sources.modifiers` (`rename` plus a matching `exclude` on the other polarity).
    `stage.ps1` guards that both declarations exist for each.
  The two axes reach different templates. `--no-aspire` is mmca-app's alone (only the app owns an
  orchestration project), so the `aspire` / `aspireSqlServer` markers are STRIPPED in the module and
  slice trees via `Convert-TemplateMarkers -StripLabels $moduleStripLabels`. `--database` reaches
  **`mmca-module` too**: a module's EF configurations inherit an engine-specific base and its
  migrations project references an engine-specific provider, so a SQL-Server-shaped module dropped
  into a sqlite app does not compile. `templates/mmca-module/.template.config/template.json` therefore
  declares the same `database` / `useSqlite` / `engineName` / `engineNameUpper` symbols in its own
  right (a condition on a symbol a template does not declare is silently false, which is why
  `stage.ps1` asserts all five declarations), its `sqlserver` markers are converted rather than
  stripped, and its printed wire-up instructions come in two `manualInstructions` entries, the sqlite
  one conditioned on `useSqlite`. The slice templates carry no engine coupling at all.
- **A generated module is never the solution's `Default` data source, and mmca-module's staged
  design-time factory is rewritten to say so** (`$moduleOnlyRewrites` in `stage.ps1`). The seed's
  factory names the same connection top level and under its own logical name, which makes the resolver
  collapse it onto `Default` and scaffold the Default-source-only framework tables (`ScheduledJobs`)
  into its migrations: right for the FIRST module, wrong for every later one. EF refuses to migrate a
  database whose model has pending changes, so a second module scaffolded that way stops the host at
  startup. The module-only copy declares its own source and no top-level connection. Its other half is
  in `build/add-module.ps1`: the orchestration host's data-source call is inserted **above** the
  existing one (`Add-BeforeAnchor`), because every such call also rewrites the top-level connection
  string and the last one wins, so the first module has to keep the `Default` role.
- **XML comments cannot contain a double hyphen**, so an option name written as `--no-aspire` inside a
  `.props` / `.csproj` / `.slnx` comment makes the whole file invalid XML. MSBuild does not fail on an
  unparsable OPTIONAL import, it skips it, which is how the overlay's `local.props` sat broken (every
  `--local-mmca` app silently built in package mode) with a green smoke run. Spell option names without
  their dashes in XML prose; `stage.ps1` guard 8 parses every staged XML file and throws.
- **A comma-separated list cannot lose one element to a whole-line region.** C# forbids a trailing
  comma in an invocation, a parameter list, and a positional record, so `--no-description` /
  `--no-owner` cannot drop a middle or last argument with markers. The seed keeps those lists on ONE
  line (the factory, the private constructor, two positional records, the create call sites, the
  `LoggerMessage` template, the typed API client's signatures and anonymous bodies), and
  `stage.ps1`'s `Convert-OptionalAxisLine` rewrites each into one conditional block per combination,
  DERIVING every variant by removing the axis's own segment. Its table is `$optionalAxisLines`: it
  throws on a missing file, an unexpected hit count, or a segment that no longer matches. Reshaping
  the seed is still preferred where it works, which is why the test object initializers carry a
  trailing comma on every member and the two UI pages accumulate their required-field check one line
  at a time.
- **Generated apps ship their own wire-up script**, `build/add-module.ps1`, laid in by
  `build/templates/overlay/mmca-app/build/`. It runs `dotnet new mmca-module` and then performs all
  seven wire-ups that template can only print (solution entries, host + architecture-test project
  references, the identifier-alias link, the map lines, `AddErrorResources`, the AppHost database
  and data-source routing, the `appsettings.json` normalization) plus the first EF migration. It is
  **`copyOnly`, and its leak check is INVERTED** relative to the razor wrappers': because it ships
  verbatim (so the `--title` / `--event-verb` / `--child` flag names it passes through survive), it
  may use any hazard word in prose, but it must contain **no seed noun at all**: at run time inside
  a generated app, `MMCA.Helpdesk` / `Helpdesk` / `Ticket` name nothing. It discovers the solution
  file (which is also the app namespace), the existing modules, and the host / test projects by
  glob. `stage.ps1` guards the copyOnly declaration, the overlay landing, and the no-seed-noun rule.
  `smoke.ps1` adds its second module THROUGH this script, so the wire-ups get CI coverage on the
  same code path adopters run.
- **`Comment` is a rename token, so the word is a hazard.** `--child` replaces the substring
  `comment` everywhere, which is why the seed says "code notes" and "children" in prose, the
  `.editorconfig` is `copyOnly`, and `stage.ps1` escapes the ResX schema's `name="comment"`
  attribute. Staging greps the staged trees for `commented` / `commenting` and throws.
- `MMCA.Templates` is deliberately **not** named `MMCA.Common.Templates`: that prefix carries the
  ADR-016 lockstep-versioning contract and ships from the MMCA.Common repo. This one ships from here
  on its own cadence and pins the framework version as a template parameter.
- Releasing it is a `templates-vX.Y.Z` tag, which runs `.github/workflows/release-templates.yml`.
  **That filename is load-bearing**: nuget.org trusted publishing is keyless OIDC pinned to
  owner + repo + workflow filename, with no API-key fallback.

## Build, test, run

This scaffold defaults to **local-source mode**: `local.props` (committed in this seed, unlike Store/ADC,
where it is gitignored) sets `UseLocalMMCA=true` and points at `../MMCA.Common/Source`, so the
`MMCA.Common.*` packages resolve via `ProjectReference` to the framework source: no GitHub Packages
token needed. The
`PackageReference`→`ProjectReference` swap lives in `Directory.Build.targets`; `nuget.config` only lists
nuget.org. To consume published packages instead, just delete `local.props`: since v1.128.0 the
`MMCA.Common.*` packages are published to **nuget.org** (ADR-053), so no extra feed and no token are
needed. **Building in local-source mode requires `../MMCA.Common/Source` to exist on disk.**

```bash
dotnet build MMCA.Helpdesk.slnx                       # warning-free under all analyzers
dotnet test  --solution MMCA.Helpdesk.slnx            # 118 tests (domain + application + architecture), NO database needed
dotnet run --project Source/Hosting/MMCA.Helpdesk.AppHost   # interactive terminal ONLY, see caveat below
```

Single test: target the project and pass a Microsoft Testing Platform filter after `--` (NOT VSTest
`--filter`):

```bash
dotnet test --project Tests/Modules/Tickets/MMCA.Helpdesk.Tickets.Domain.Tests/MMCA.Helpdesk.Tickets.Domain.Tests.csproj -- --filter-method "*Create_WithEmptyTitle*"
dotnet test --project Tests/Architecture/MMCA.Helpdesk.Architecture.Tests/MMCA.Helpdesk.Architecture.Tests.csproj -- --filter-class "*ModuleIsolationTests*"
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
`AddApplication()` → `AddInfrastructure()` → the three v1.150.0 opt-ins that layer on top of it
(`AddAuditTrail()` → `AddScheduledJobs()` → `AddMultiTenancy()`, order irrelevant among themselves)
→ `AddAPI(modulesSettings)` →
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

**The caching pair is wired, not just available:** `GetTicketByIdQuery` implements `IQueryCacheable`
and every ticket command implements `ICacheInvalidating`, both keyed through `TicketCacheKeys` (the
one place the module names cache entries). The decorator matches the two by **string prefix**, so a
read key that drifts out of `TicketCacheKeys.Prefix` goes stale silently: `TicketCacheInvalidationTests`
is what catches that, plus the two rules worth remembering (invalidation runs only on success, and
outside the transaction). Keep both halves pointing at `TicketCacheKeys`.

**Two event paths, deliberately distinct:** *domain events* (`TicketChanged`) are intra-module, dispatched
synchronously post-save; *integration events* (`TicketOpenedIntegrationEvent`) go through the outbox
(`IEventBus.PublishAsync`): in-process in the monolith, over a broker once Tickets is
extracted, with no handler change (ADR-003 / ADR-008). Integration events carry a `SchemaVersion`
(ADR-010).

**Use cases / wiring:** handlers implement `ICommandHandler<,>` / `IQueryHandler<,>` and are
**convention-scanned by Scrutor** via `ScanModuleApplicationServices<ClassReference>()` in the module's
`Application/DependencyInjection.cs` (which uses the C# `extension(IServiceCollection)` syntax): you do
not register each handler by hand. The plain-CRUD write side is the framework's own: one
`AddEntityCrud<Ticket, ...>()` call closes the generic create/update/delete handlers over the module's
types, the mutation stays on the aggregate behind `TicketUpdateApplier` (an `IEntityUpdateApplier`,
scanned like the create mapper), and the controller constructs `UpdateEntityCommand<...>` directly.
**Three ordering facts are load-bearing and silent**: `AddEntityCrud` runs AFTER the scan (it uses
`TryAdd`, so `CreateTicketHandler` keeps the create verb and `TicketUpdateHandler`, which exists only
to eager-load the children the response carries, keeps the update verb); the `CommandRequestValidator`
bridge for the closed framework command is registered by hand (the framework's automatic bridge only
sees commands declared in the module assembly, so `TicketUpdateRequestValidator` would otherwise stop
running); and `DeleteTicketHandler` stays hand-written with its own command, because it loads the
children so `Delete()` can cascade and because it is the source of the `mmca-command` slice template.
`TicketsCrudRegistrationTests` pins all of it. Read endpoints come from `EntityControllerBase`; writes
inject handlers directly into `TicketsController`. Mapping is **manual via Mapperly** source-generators
(`*DTOMapper`, `*RequestMapper`), not AutoMapper (ADR-001). Failures map to RFC 9457 ProblemDetails
through `HandleFailure`.

**Persistence:** `ModuleApplicationDbContext` is abstract and only declares the module's `DbSet`s; the
concrete runtime context is the single `SQLServerDbContext` from MMCA.Common: **one instance per
database, never a per-module context class** (ADR-006). EF entity configurations are auto-discovered by
assembly-name convention, which is why `Infrastructure/DependencyInjection.cs` is a near no-op.

**UI** (`Source/Hosts/UI/...`): Blazor Server + MudBlazor calling the API **server-side** via the typed
`HelpdeskApiClient` + Aspire service discovery (base address `https+http://web` from config): no
browser CORS, no token. **It throws nothing for a server answer**: every method returns `Result` /
`Result<T>`, the same contract `EntityServiceBase` honors, composed from the two framework pieces
`ProblemDetailsResultReader` (reads the RFC 9457 body into errors, so a failure carries whatever
invariant the aggregate refused on) and `HttpResultExecutor` (turns a fault with no response at all,
connection refused or timeout, into a failure too). Only the caller's own `OperationCanceledException`
still propagates, and `GetTicketAsync` reports a missing ticket as a `NotFound` failure rather than a
null. The two pages therefore **branch instead of catching**, using the ergonomics in
`MMCA.Common.UI.Common` (`TryGetValue` to unwrap, `LocalizedErrorMessage` to compose the snackbar
text through the page's own localizer); `_Imports.razor` carries that namespace.

## Multi-tenancy demo (and the other v1.150.0 opt-ins)

This seed is the framework's **runnable reference for multi-tenancy**, so unlike ADC and Store it turns
the feature on. `Ticket` and `TicketComment` implement `ITenantEntity`, which is the whole domain-side
change: the framework adds a required `varchar(64)` `TenantId` column plus an index to each table, and
composes a named `Tenant` query filter with the existing `SoftDelete` one. `Ticket` also implements
`IAuditedEntity` (field-level change history in `dbo.AuditTrailEntries`, written in the same
transaction as the data); `TicketComment` deliberately does not, because the trail records history per
aggregate.

**Two tenants, on purpose, because there are two isolation modes:**

- **`acme`** carries no `Tenancy:Tenants:acme:DataSources` override, so it shares the pooled Helpdesk
  database and is separated from other tenants only by the query filter (shared schema).
- **`globex`** overrides the connection string and lands in its own `Helpdesk_Globex` database
  (database per tenant). The AppHost declares that second database and injects its connection string
  as `Tenancy__Tenants__globex__DataSources__Default__SQLServerConnectionString`; the host migrates it
  at startup in the per-tenant pass, and the outbox drains per `(source, tenant)` pair.

Two details that look wrong until you know them:

- **The override key is `Default`, not `Tickets`.** Per-tenant overrides are keyed by **physical** data
  source name. The AppHost injects the same connection string as both the `Tickets` logical source and
  the top-level one, so the resolver collapses `Tickets` onto `Default`. Keying the override `Tickets`
  fails `ValidateOnStart` with "overrides a SQLServer data source that does not exist". The design-time
  factory mirrors that collapse for exactly the same reason: `ScheduledJobs` is a Default-source-only
  table, so a factory whose physical source is not `Default` scaffolds a migration without it.
- **`RequireTenant` is `false` here, against the framework default of `true`.** This seed runs
  issuer-less (no Identity module, `[AllowAnonymous]` endpoints), so no request carries a `tenant_id`
  claim and fail-closed would answer every unheadered call, including the health probes' neighbours and
  the guides' first `curl`, with a 400. `false` makes an unresolved caller the **system** caller, which
  reads across all tenants. Any app with a real issuer should delete the line and take the fail-closed
  default. The trade-off is not free: an unresolved caller can read every tenant's rows, and a **write**
  with no tenant resolved throws `CrossTenantWriteException` rather than inserting an untenanted row
  (the column is NOT NULL). That is why the Blazor UI stamps the header rather than relying on the
  permissive default.

**Exercising it:** resolution is claim first, then the `X-Tenant-Id` header. `curl -H "X-Tenant-Id:
acme" .../Tickets` and the same call with `globex` hit two different databases through one code path.
The UI host sends the header for every call from its typed client, from `Api:TenantId` (default
`acme`), so switching that one config value re-points the whole front end at the other tenant.

**Adopting on existing data:** the migration adds `TenantId` with a `""` default, and `""` matches no
tenant, so pre-existing rows become invisible to every tenant while staying visible to the system
caller. A real app stamps those rows before turning `RequireTenant` on.

The other two opt-ins are configuration only. `AuditTrail:Enabled` and `Scheduler:Enabled` are both
true here, and they travel together: `AddAuditTrail` registers the `audit-trail-cleanup` retention job,
but only the scheduler runner actually runs it. Both are mirrored by `EnableAuditTrail` /
`EnableScheduler` on the design-time factory, and **those flags must stay in step with the host
settings** or the scaffolded model drifts from the running one. CSV export needed no change at all:
`EntityControllerBase` grew `GET /Tickets/export` on rebuild.

## Seed-specific gotchas

- **Issuer-less auth by default.** No Identity module ships in this seed, so `Web/Program.cs` registers
  a bare auth scheme when `Authentication:JwtBearer:Authority` is unset and `TicketsController` is
  `[AllowAnonymous]`. To add real RS256/JWKS auth: add the Identity module (common-BUILD-BY-HAND.md
  Phase 8), set the authority, and flip the controller back to `[Authorize]`. Don't add `[Authorize]`
  without an issuer or the pipeline rejects every request.
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
