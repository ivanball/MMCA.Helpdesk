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

Run the AppHost from a **real, interactive terminal**. Launched from a background or headless shell
it stalls at control-plane init: no dashboard, no browser, and it looks like a hang.

The dashboard lists three resources: `sql`, `web` (the API), and `ui` (Blazor). Open **`ui`** to use
the app. The API serves `GET/POST /Tickets`, `/health`, and `/alive`; it has no page at `/`, so the
API root returns 404 by design.

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
- **The AppHost waits on `sql`, not on the database resource.** The host creates the database via EF
  `Migrate` at startup, so `WaitFor(db)` deadlocks: the database is never healthy until it exists,
  and the only thing that creates it is the host doing the waiting.
- **Every layer assembly must be registered in `HelpdeskArchitectureMap`.** Add a module or a layer
  and forget its `Module(...)` line, and the layering and isolation rules silently stop covering it.
- **The AppHost needs its `Properties/launchSettings.json`.** Without it the Aspire dashboard
  endpoints are never configured and F5 appears to hang.
- **Every `MMCA.Common.*` version moves together.** There is no phased rollout and no per-package
  skew; `FrameworkVersionConsistencyTests` fails the build if a sweep is half finished.

## Two one-time fixups the scaffold deliberately left to you

**1. Re-sort the using directives.** Renaming every namespace moved where your own usings sort
against `MMCA.Common.*` and the third-party ones, so `SA1210` starts as a suggestion (see the
scaffold delta at the bottom of `.editorconfig`). Sort them with:

```bash
dotnet format analyzers MMCA.Helpdesk.slnx --diagnostics SA1210 --severity error
```

Every `mmca-command` / `mmca-query` slice arrives with the same skew, so either re-run that after
scaffolding, or leave the delta in place until you have stopped scaffolding and then delete it.

**2. Freeze your integration-event wire contract.** Integration events cross service boundaries over
the broker, so a renamed, removed, or retyped property breaks consumers in another service. The
framework can fail the build on a silent reshape, but only against a contract **you** froze: one
inherited from a sample module guarantees nothing. Add this to
`Tests/Architecture/MMCA.Helpdesk.Architecture.Tests/ArchitectureTests.cs`:

```csharp
public sealed class IntegrationEventContractTests : IntegrationEventContractTestsBase
{
    protected override IArchitectureMap Map { get; } = new HelpdeskArchitectureMap();

    // Frozen wire contract for the module's async API. Update DELIBERATELY when evolving an
    // integration event (version it per ADR-010; never a silent reshape).
    protected override IReadOnlyList<string> ExpectedContract =>
    [
        // paste the actual value from the failing run here
    ];
}
```

then run it once and paste the value the failure prints:

```bash
dotnet test --project Tests/Architecture/MMCA.Helpdesk.Architecture.Tests/MMCA.Helpdesk.Architecture.Tests.csproj \
  -- --filter-class "*IntegrationEventContract*"
```

## Adding to it

A whole module across all five layers, plus its test and migrations projects:

```bash
dotnet new mmca-module -n Billing --app MMCA.Helpdesk --aggregate Invoice
```

`mmca-module` prints the six wire-ups it cannot perform for you (the solution entries, the host and
architecture-test project references, the identifier-alias link, the architecture-map lines, the
host's `AddErrorResources` call, and the module's own database: an AppHost database resource plus a
`DataSources` entry per module, with the top-level migrations-assembly pin removed). Until they are
done the module is invisible to the host and to the fitness rules.

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
