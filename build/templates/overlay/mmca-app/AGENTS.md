# AGENTS.md

Guidance for every coding agent working in this solution (Claude Code imports this file from
`CLAUDE.md`; Copilot, Codex and Cursor read it directly). The solution was scaffolded with
`dotnet new mmca-app` on the MMCA.Common framework: .NET 10, DDD, Clean Architecture, CQRS, a
modular monolith whose modules can later be extracted into services without a rewrite.

Below, `<App>` is this solution's root namespace (the `.slnx` file's base name), `<Module>` a module
folder under `Source/Modules/`, and `<Aggregate>` its aggregate root. Discover them from the tree;
never guess them.

## Build and test

```bash
dotnet build <App>.slnx                  # warning-free: warnings are errors, five analyzers at error severity
dotnet test  --solution <App>.slnx       # domain + application + architecture tests, no database needed
dotnet test --project Tests/Modules/<Module>/<App>.<Module>.Domain.Tests/<App>.<Module>.Domain.Tests.csproj -- --filter-method "*MethodPattern*"
```

The tests run on Microsoft Testing Platform, not VSTest: a single test is targeted by project plus a
filter after `--` (`--filter-method` or `--filter-class`). A bare `--filter` matches nothing and
reports zero tests. Run the Aspire AppHost only from an interactive terminal; launched headless it
stalls at startup.

## Add code through the templates, not by hand

The `MMCA.Templates` pack generates every shape this solution grows by. A hand-written slice or
module compiles and still misses wiring that the templates get right and the tests check.

- **A whole module**: from the solution root, `pwsh build/add-module.ps1 -Name <Module> -Aggregate <Aggregate>`
  (run it with `-?` for the shape options). It drives `dotnet new mmca-module` and then performs
  the wire-ups the template can only print: solution entries, host and architecture-test
  references, the identifier-alias link, the architecture-map lines, error resources, the module's
  database and data-source routing, and the first EF migration. `dotnet new mmca-module` on its own
  leaves all of those to you.
- **A command (write) slice**: from `Source/Modules/<Module>/<App>.<Module>.Application/<Module>/UseCases`,
  `dotnet new mmca-command -n <UseCase> --app <App> --module <Module> --aggregate <Aggregate> --domain-method <Method>`.
  Add `--child-collection <Navigation>` only when the handler must eager-load a child collection.
- **A query (read) slice**: from the same folder,
  `dotnet new mmca-query -n <UseCase> --app <App> --module <Module> --aggregate <Aggregate>`.

Generate slices from that exact folder: the command template also writes a cache-invalidation test
builder relative to it, into the module's Application test project. The `.claude/skills/slice`
skill walks through the whole sequence.

## Framework rules that fail quietly when broken

- **Every handler registration goes inside `AddMmcaApplicationPipeline(...)`** in the API host's
  `Program.cs`. The call runs `AddApplication()`, then its `Register` steps, then
  `AddApplicationDecorators()` last, and seals the pipeline; decorators wrap only handlers that
  already exist. Non-handler registrations (`AddInfrastructure`, `AddAPI`, health checks) stay
  outside it. Handlers are convention-scanned, so a new slice needs no DI line at all.
- **The decorator order is fixed by the framework** (commands: view-as guard, feature gate,
  authorization, logging, caching, validation, timeout, transaction, handler; queries drop the
  view-as guard and the transaction). Do not reorder it or wrap a handler by hand.
- **`AddEntityCrud<...>()` runs after the module's convention scan** in the module's Application
  `DependencyInjection.cs`. It registers with `TryAdd`, so a hand-written handler for the same verb
  wins only when it was scanned first.
- **Cache keys come from one builder per aggregate**, `<Aggregate>CacheKeys` in
  `<App>.<Module>.Application/<Module>/`. A query's `IQueryCacheable.CacheKey` and a command's `ICacheInvalidating.CachePrefix`
  are matched by string prefix, so both must be built from `<Aggregate>CacheKeys.Prefix`; a key that
  drifts outside it is never evicted and nothing fails. Every `ICacheInvalidating` command needs a
  test builder (an `ICacheInvalidatingCommandBuilder` in the module's Application test project, or
  an entry in the cache-invalidation test's hand-written table); the test fails by name without one.
- **Every data-carrying command has a FluentValidation validator.** `CommandValidatorCoverageTests`
  fails the build otherwise. Validators check input shape; business rules live on the aggregate.
- **Errors are `Result` / `Result<T>`, not exceptions.** Aggregates are created through a static
  `Create(...)` factory returning `Result<T>`, never a public constructor, with invariants composed
  through `Result.Combine`.
- **Every module entity is tenant-scoped (`ITenantEntity`) or declared global** in the architecture
  tests' `GlobalEntityTypes` with a reason. Add the marker unless every tenant truly shares the rows.
- **Integration events are a frozen wire contract.** Version an event deliberately and update
  `ExpectedContract` in `IntegrationEventContractTests` in the same change.
- **Every `MMCA.Common.*` package version moves together** in `Directory.Packages.props`.

## Folders and naming

- Feature by folder, use case by leaf: Domain, Application and Shared lead with the feature folder
  (`<Module>/`); API, UI and Infrastructure lead with a technical root (`Controllers/`, `Pages/`,
  `Persistence/`). Follow the layout the module already has rather than inventing a second one.
  One use case per `<Module>/UseCases/<UseCase>/` folder.
- At most 12 direct code files per folder (`FolderWidthTests`); split by concern before that.
- Never name a sub-folder `Domain`, `Application`, `Infrastructure`, `API` or `UI` inside a module
  project: the module name is derived from those namespace segments.
- Namespaces follow folders. File-scoped namespaces, braces always, `_camelCase` private fields,
  `var` only when the type is apparent.
- Every new layer assembly is registered in the architecture map under `Tests/Architecture/`, or
  the layering rules silently stop covering it (`build/add-module.ps1` does this for a module).

## Never

- Never add a per-module `DbContext`: one context class per database engine, one instance per database.
- Never reference a broker SDK from Domain, Application or Shared; use `IEventBus` and the outbox.
- Never weaken an architecture test, an analyzer rule or an allowlist to make a build pass. Fix the code.
