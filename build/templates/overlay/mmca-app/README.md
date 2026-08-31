# MMCA.Helpdesk

A modular monolith on the [MMCA.Common](https://www.nuget.org/packages?q=MMCA.Common) framework:
.NET 10, DDD, Clean Architecture, and CQRS, scaffolded with `dotnet new mmca-app`.

One business module (**Tickets**) is wired end to end through all five layers, behind a REST API
host and a Blazor Server + MudBlazor UI host, orchestrated by Aspire. The framework's promise is
that you build this monolith now and extract a module into its own service later, without a rewrite.

## Build, test, run

```bash
dotnet build MMCA.Helpdesk.slnx                 # warning-free: five analyzers at error severity
dotnet test  --solution MMCA.Helpdesk.slnx      # domain + application + architecture, NO database needed
dotnet run --project Source/Hosting/MMCA.Helpdesk.AppHost
```

Check your migrations before that third line runs for the first time: see the next section.

Run the AppHost from a **real, interactive terminal**. Launched from a background or headless shell
it stalls at control-plane init: no dashboard, no browser, and it looks like a hang.

The dashboard lists `web` (the API) and `ui` (Blazor), plus a `sql` container when the solution was
scaffolded against SQL Server (SQLite is an in-process file and has no resource of its own). Open
**`ui`** to use the app. The API serves `GET/POST /Tickets`, `/health`, and `/alive`; it has no page
at `/`, so the API root returns 404 by design.

### Before the first run: your own first migration

Look inside `Source/Hosting/MMCA.Helpdesk.Migrations.SqlServer.Tickets/Migrations/`. If the only
file there is `.editorconfig`, this solution was scaffolded with a shape flag or with `--database
sqlite`, and the sample migrations were deliberately dropped: they describe a schema you did not
ask for. Create your own **before** the first run:

```bash
dotnet ef migrations add InitialCreate \
  --project Source/Hosting/MMCA.Helpdesk.Migrations.SqlServer.Tickets \
  --startup-project Source/Hosting/MMCA.Helpdesk.Migrations.SqlServer.Tickets \
  --context SQLServerDbContext
```

This is required rather than tidy-up. The API host **migrates** its data sources at startup, it does
not create them outright, so a migrations assembly with nothing in it leaves you with an empty
database and a first request that fails on a missing table. Once the migration exists, every later
one applies the same way: add it and restart the host.

`dotnet ef` is a global tool rather than part of the SDK. Install it with `dotnet tool install
--global dotnet-ef` if the command is not found. The design-time factory in that project opens no
connection for `migrations add`, so this needs no running database.

To run a single test class or method, target the project and pass a Microsoft Testing Platform
filter after `--`. These solutions run on MTP, not VSTest, so a bare `--filter` silently matches
nothing and exits reporting zero tests:

```bash
dotnet test --project Tests/Modules/Tickets/MMCA.Helpdesk.Tickets.Domain.Tests/MMCA.Helpdesk.Tickets.Domain.Tests.csproj -- --filter-method "*Create_WithEmptyTitle*"
dotnet test --project Tests/Architecture/MMCA.Helpdesk.Architecture.Tests/MMCA.Helpdesk.Architecture.Tests.csproj -- --filter-class "*ModuleIsolationTests*"
```

## Layout

```
MMCA.Helpdesk.slnx
Directory.Build.props        analyzers, language settings, the module identifier-alias links
Directory.Build.targets      the local-source PackageReference -> ProjectReference swap
Directory.Packages.props     Central Package Management: every version lives here
.editorconfig                drives the five analyzers
Source/
  Modules/Tickets/           Shared, Domain, Application, Infrastructure, API
  Hosts/MMCA.Helpdesk.Web    the monolith REST API host
  Hosts/UI/MMCA.Helpdesk.UI.Web   Blazor Server + MudBlazor
  Hosting/                   Aspire AppHost + one migrations project per (future) service database
Tests/
  Modules/Tickets/           domain + application tests
  Architecture/              the fitness functions, parameterized by HelpdeskArchitectureMap
```

## Things that are load-bearing and quiet about it

- **`AddApplicationDecorators()` must be the last DI call** in `Source/Hosts/MMCA.Helpdesk.Web/Program.cs`.
  Decorators wrap handlers that already exist, and the module handlers are registered by
  `ModuleLoader`. Move it earlier and the pipeline silently stops wrapping them.
- **`AddEntityCrud<...>()` must come AFTER the module's convention scan**, in
  `Source/Modules/Tickets/MMCA.Helpdesk.Tickets.Application/DependencyInjection.cs`. It registers the
  framework's create, update and delete handlers with `TryAdd`, which is what lets the module keep a
  verb it wrote itself; above the scan it would take those verbs instead, with no error. Ordering
  matters for the validator bridge too: the same call registers the `CommandRequestValidator` for the
  closed `UpdateEntityCommand` (the module scan's own bridge only sees commands declared in the module
  assembly, and that command is declared in MMCA.Common), again with `TryAdd`, so a scan-registered
  explicit validator still wins. Without that registration the `*UpdateRequestValidator` silently
  stops running. `TicketsCrudRegistrationTests` pins both.
- **On SQL Server the AppHost waits on `sql`, not on the database resource.** The host creates the
  database via EF `Migrate` at startup, so `WaitFor(db)` deadlocks: the database is never healthy
  until it exists, and the only thing that creates it is the host doing the waiting. A SQLite
  solution waits on nothing, because there is no server to come up.
- **Every layer assembly must be registered in `HelpdeskArchitectureMap`.** Add a module or a layer
  and forget its `Module(...)` line, and the layering and isolation rules silently stop covering it.
- **The AppHost needs its `Properties/launchSettings.json`.** Without it the Aspire dashboard
  endpoints are never configured and F5 appears to hang.
- **Every `MMCA.Common.*` version moves together.** There is no phased rollout and no per-package
  skew; `FrameworkVersionConsistencyTests` fails the build if a sweep is half finished.

## The one-time fixup the scaffold deliberately left to you

**Re-sort the using directives.** Renaming every namespace moved where your own usings sort
against `MMCA.Common.*` and the third-party ones, so `SA1210` starts as a suggestion, and `SA1211`
with it: the identifier-alias file's two aliases sort differently depending on the aggregate and
child names you asked for (see the scaffold delta at the bottom of `.editorconfig`). Sort them with:

```bash
dotnet format analyzers MMCA.Helpdesk.slnx --diagnostics SA1210 SA1211 --severity info
```

Every `mmca-command` / `mmca-query` slice arrives with the same skew, so either re-run that after
scaffolding, or leave the delta in place until you have stopped scaffolding and then delete it.

The same delta relaxes `IDE0021` for a different reason: the aggregate's private constructor assigns
one property per optional axis, so turning several of them off can leave it with a single statement,
which the baseline would otherwise require you to write as an expression body. Fold it by hand, or
add your own second property, and delete that line too.

## Your integration-event wire contract is already frozen

`IntegrationEventContractTests` in
`Tests/Architecture/MMCA.Helpdesk.Architecture.Tests/ArchitectureTests.cs` ships holding **your**
event, under the names you scaffolded with, and it passes on arrival. Integration events cross
service boundaries over the broker, so a renamed, removed, or retyped property breaks consumers in
another service; that test is what turns such a reshape into a failing build rather than a runtime
surprise in someone else's service.

It compares member lists as a set, so reordering two properties is free. Everything a consumer can
observe is not: a missing member, an extra member, a changed type, and any change to the set of
events itself. When you add an event or evolve one on purpose, version it (ADR-010), then update
`ExpectedContract` in the same commit. The failure prints the live value to paste:

```bash
dotnet test --project Tests/Architecture/MMCA.Helpdesk.Architecture.Tests/MMCA.Helpdesk.Architecture.Tests.csproj \
  -- --filter-class "*IntegrationEventContract*"
```

## Adding to it

### A whole module: one command

```bash
pwsh build/add-module.ps1 -Name Billing -Aggregate Invoice
```

That scaffolds the module across all five layers, adds its test and migrations projects, and then
performs every wire-up `dotnet new` cannot reach: the solution entries, the host and
architecture-test project references, the identifier-alias link, the architecture-map lines, the
host's error-resource registration, the module's own Aspire database and its data-source routing,
the `appsettings.json` normalization that a second module needs, and the module's first EF
migration.

Run it from the solution root. It refuses to run anywhere else, and everything else about this
solution (its name, the module already here, its hosts and its test projects) it discovers at run
time, so nothing in it is wired to the names you scaffolded with. It accepts the same shape options
as the template it drives and passes them through, so **run it with `-?` for every option**.

Each edit is anchored on something the scaffold generated, and a missing anchor stops the run
printing the edit to make by hand instead: a half-wired solution is never the quiet outcome. A name
that is already under `Source/Modules` is refused before anything is generated, and any step that
was already done is skipped with a note, so a run that died part way can be fixed and rerun.

Two things it deliberately leaves to you: the Blazor UI pages (the scaffold's UI host still shows
only the first module), and the new module's page-level localization resources.

### The template on its own

Usable directly in a solution this scaffold did not generate. It prints the seven wire-ups it
cannot perform:

```bash
dotnet new mmca-module -n Billing --app MMCA.Helpdesk --aggregate Invoice
```

### Shaping the sample module

Both `mmca-app` and `mmca-module` generate an aggregate with four optional axes, and five options
decide what you get. They are shape decisions, not toggles: the code for an axis you turn off is
never generated, so there is nothing to delete afterwards.

| Option | Effect |
|---|---|
| `--flat` | No child collection at all: no child entity, DTO, requests, mapper, EF configuration, Add/Edit/Remove slices, controller endpoints, identifier alias, or tests. Use it when the aggregate owns no growable children. |
| `--no-status` | No status axis: no status enum, no `ChangeStatus` slice, request, or endpoint, no `Status` property, no status invariant or tests. Use it when the aggregate has no lifecycle state. |
| `--no-description` | No long-text property: no `Description` property or invariant, no max-length constant, no DTO, request, or command field, no validator rule, no EF max-length configuration, no error-resource entries, no UI field, no tests. Use it when the aggregate's main text property is all the text it needs. |
| `--no-owner` | No owning-user property: no `RequesterUserId` property, no create-request field or validator rule, no member on the creation integration event, no EF index, no UI field or column, no tests. Use it when the aggregate is not owned by a single user, or until you add an Identity module to resolve one. |
| `--child <Name>` | Renames the child concept. `--aggregate Order --child Item` gives you an `OrderItem` entity, `AddItem` / `EditItem` / `RemoveItem` slices, and `/items` routes. Ignored under `--flat`. |

```bash
dotnet new mmca-app -n Contoso.Support --module Orders --aggregate Order --child Line
dotnet new mmca-app -n Contoso.Catalog --module Products --aggregate Product --flat --no-status --no-owner
dotnet new mmca-module -n Shipping --app MMCA.Helpdesk --aggregate Shipment --flat --no-status
```

### Shaping the solution

Two more `mmca-app` options decide the shape of the solution rather than of the module, and they
compose with each other and with everything above.

| Option | Effect |
|---|---|
| `--database sqlserver\|sqlite` | The relational engine. `sqlite` gives you one file and no server: entity configurations inherit `EntityTypeConfigurationSqlite`, the migrations project is `<App>.Migrations.Sqlite.<Module>` against `Microsoft.EntityFrameworkCore.Sqlite`, the API host requires a database rather than SQL Server specifically, and the app is single-tenant (database per tenant needs a second server-backed database). Default `sqlserver`. |
| `--no-aspire` | No orchestration project: no AppHost, no `Aspire.Hosting.*` pins, and the UI host reaches the API at a fixed `https://localhost:60801` instead of through service discovery. Both hosts keep `AddServiceDefaults()` (OpenTelemetry, `/health` + `/alive`, resilience), so adding an AppHost later is additive rather than a rewrite. |

```bash
dotnet new mmca-app -n Contoso.Notes --module Notes --aggregate Note --database sqlite --no-aspire --flat
```

That is the smallest shape the template produces: two hosts, one module, one `notes.db` file, and
`dotnet run` on each host. `--database sqlite` also drops the sample migrations for the same reason
the shape flags do (they are SQL Server DDL), so run `dotnet ef migrations add InitialCreate`
against the project the scaffold generated, before the first run rather than after it (see Before
the first run above).

Two things worth knowing. The plural forms are derived by a simple English pluralizer (`Line` ->
`Lines`, `Entry` -> `Entries`, `Box` -> `Boxes`), so an irregular noun needs one rename by hand. And
passing any of the four shape flags to `mmca-app` drops the sample migrations, because they describe
the full shape: run `dotnet ef migrations add InitialCreate` against the shape you actually asked
for, before the first run.

`mmca-module` prints the six wire-ups it cannot perform for you (the solution entries, the host and
architecture-test project references, the identifier-alias link, the architecture-map lines, the
host's `AddErrorResources` call, and the module's own database: an AppHost database resource plus a
`DataSources` entry per module and an explicit `Outbox` source, with the top-level
migrations-assembly pin removed). Until they are done the module is invisible to the host and to the
fitness rules.

A single vertical slice, run from the module's `UseCases` folder:

```bash
cd Source/Modules/Tickets/MMCA.Helpdesk.Tickets.Application/Tickets/UseCases

dotnet new mmca-command -n ArchiveTicket --app MMCA.Helpdesk --module Tickets \
  --aggregate Ticket --domain-method Archive

dotnet new mmca-query -n GetTicketByNumber --app MMCA.Helpdesk --module Tickets \
  --aggregate Ticket
```

Handlers are convention-scanned, so there is no DI registration to add. Add the `--domain-method` to
your aggregate before the command slice compiles, and keep the query's `CacheKey` inside your
module's `*CacheKeys.Prefix`: the caching decorator matches reads to invalidating commands by string
prefix, so a key that drifts out of it goes stale silently.

The command slice arrives with a validator beside its command, asserting that the identifier it
carries is a real one. Keep it and grow its rules as the command grows a payload: the solution's
`CommandValidatorCoverageTests` fails any handled command that carries data and has no validator, so
deleting it turns your next test run red.

## Where to look next

- **[Getting started](https://ivanball.github.io/docs/guides/common-GETTING-STARTED.html)**: the
  six-step path from this solution to a running, migrated app.
- **[Building by hand](https://ivanball.github.io/docs/guides/common-BUILD-BY-HAND.html)**: what each
  generated file does and why, phase by phase.
- **[Template reference](https://ivanball.github.io/docs/guides/common-TEMPLATES.html)**: every
  parameter of all four templates.
- **[The ADRs](https://ivanball.github.io/docs/adr/README.html)**: the reasoning behind each pattern.
- **[MMCA.Helpdesk](https://github.com/ivanball/MMCA.Helpdesk)**: the reference app this solution was
  generated from, kept build- and test-verified.
