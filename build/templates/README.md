# MMCA.Templates

`dotnet new` templates for the [MMCA.Common](https://www.nuget.org/packages?q=MMCA.Common)
framework: .NET 10, DDD, Clean Architecture, and CQRS, built as a modular monolith you can extract
services out of later without a rewrite.

```bash
dotnet new install MMCA.Templates
dotnet new mmca-app -n Contoso.Support --module Orders --aggregate Order
cd Contoso.Support
dotnet build Contoso.Support.slnx
dotnet test  --solution Contoso.Support.slnx
```

That generates a warning-free, test-passing, migration-ready solution: one business module across
all five layers, a REST API host, a Blazor Server + MudBlazor UI host, an Aspire AppHost, a
per-database migrations project, and domain, application, and architecture-fitness test projects.

## Templates

| Short name | Generates |
|---|---|
| `mmca-app` | the whole solution: build plumbing, one module, both hosts, the AppHost, migrations, tests |
| `mmca-module` | a new business module across all five layers, plus its test and migrations projects |
| `mmca-command` | one write-side vertical slice inside an existing module: command record + handler |
| `mmca-query` | one read-side vertical slice inside an existing module: cacheable query record + handler |

## `mmca-app` parameters

| Parameter | Default | Meaning |
|---|---|---|
| `-n, --name` | `MMCA.App` | solution and root namespace, for example `Contoso.Support` |
| `-m, --module` | `Tickets` | the first business module, plural PascalCase |
| `-a, --aggregate` | `Ticket` | that module's aggregate root, singular PascalCase |
| `-c, --child` | `Comment` | the aggregate's child entity, singular PascalCase; the type is `<aggregate><child>`, so `--aggregate Order --child Item` gives `OrderItem`, `AddItem` / `EditItem` / `RemoveItem` slices, and `/items` routes |
| `--flat` | off | generate no child collection at all: no child entity, DTO, requests, mapper, EF configuration, Add/Edit/Remove slices, endpoints, alias, or tests (makes `--child` irrelevant) |
| `--no-status` | off | generate no status axis: no status enum, no `ChangeStatus` slice, request, or endpoint, no `Status` property, no status invariant or tests |
| `-f, --framework-version` | the version this pack was built against | the `MMCA.Common.*` version to pin (all packages move together) |
| `--local-mmca` | off | build against `../MMCA.Common/Source` instead of the published packages |
| `--no-restore` | off | skip the restore after generation |

`--flat` and `--no-status` are shape decisions, not toggles: the code for an axis you turn off is
never generated, so there is nothing to delete afterwards. Passing either also drops the sample
migrations, because they describe the full shape; run `dotnet ef migrations add InitialCreate`
against the shape you asked for. Plural forms are derived by a simple English pluralizer (`Item` ->
`Items`, `Entry` -> `Entries`, `Box` -> `Boxes`), so an irregular noun needs one rename by hand.

## `mmca-module` parameters

Run this from your solution root. It prints the seven wire-ups it cannot perform for you.

| Parameter | Default | Meaning |
|---|---|---|
| `-n, --name` | `Sample` | the module, plural PascalCase; names the folders, projects, and namespaces |
| `--app` | required | your solution / root namespace |
| `-a, --aggregate` | required | the module's aggregate root, singular PascalCase |
| `-c, --child` | `Comment` | the aggregate's child entity, as above |
| `--flat` | off | as above |
| `--no-status` | off | as above |

## `mmca-command` and `mmca-query` parameters

Run these from an existing module's `UseCases` folder.

| Parameter | Applies to | Default | Meaning |
|---|---|---|---|
| `-n, --name` | both | | the use case, PascalCase; names the folder, the namespace, and both types |
| `--app` | both | required | your solution / root namespace |
| `-m, --module` | both | required | the module the slice goes into |
| `-a, --aggregate` | both | required | the aggregate the handler loads |
| `--domain-method` | `mmca-command` | `Delete` | the guarded method the command calls on the aggregate |
| `--child-collection` | both | unset | navigation to eager-load, for example `Lines`; unset loads the aggregate root alone |

`--domain-method` is the one thing the scaffold cannot invent. Add that method to your aggregate
returning `Result` before the slice compiles, or you get
`'Order' does not contain a definition for 'Cancel'`.

Full reference: <https://ivanball.github.io/docs/guides/common-TEMPLATES.html>.
Getting started (six steps to a running app):
<https://ivanball.github.io/docs/guides/common-GETTING-STARTED.html>.
What the generated code does, phase by phase:
<https://ivanball.github.io/docs/guides/common-BUILD-BY-HAND.html>.

## How this package is built

The template content is the [MMCA.Helpdesk](https://github.com/ivanball/MMCA.Helpdesk) reference
application itself, staged at pack time. There is no second copy of the solution, so the template
cannot drift from the app whose CI keeps it building.
