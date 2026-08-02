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
| `mmca-slice` | one vertical slice (command or query) inside an existing module |

## `mmca-app` parameters

| Parameter | Default | Meaning |
|---|---|---|
| `-n, --name` | `MMCA.App` | solution and root namespace, for example `Contoso.Support` |
| `-m, --module` | `Tickets` | the first business module, plural PascalCase |
| `-a, --aggregate` | `Ticket` | that module's aggregate root, singular PascalCase |
| `-f, --framework-version` | current | the `MMCA.Common.*` version to pin (all packages move together) |
| `--local-mmca` | off | build against `../MMCA.Common/Source` instead of the published packages |
| `--no-restore` | off | skip the restore after generation |

Full reference: <https://ivanball.github.io/docs/guides/common-TEMPLATES.html>.
Getting started: <https://ivanball.github.io/docs/guides/common-GETTING-STARTED.html>.

## How this package is built

The template content is the [MMCA.Helpdesk](https://github.com/ivanball/MMCA.Helpdesk) reference
application itself, staged at pack time. There is no second copy of the solution, so the template
cannot drift from the app whose CI keeps it building.
