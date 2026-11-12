---
name: slice
description: Use when asked to add a new command or query to a module, add a new use case or endpoint end to end, or add a new module, and proactively before hand-writing a handler, command or query record in this solution.
---

# Add a vertical slice

Add one command or one query to an existing module through the `MMCA.Templates` slice templates,
then finish the parts a template cannot. One use case per slice. `AGENTS.md` is the contract; read
it first.

## Steps

1. **Find the names.** `<App>` is the `.slnx` file's base name, `<Module>` a folder under
   `Source/Modules/`, `<Aggregate>` the aggregate root in
   `Source/Modules/<Module>/<App>.<Module>.Domain/<Module>/`. If the request names a module or
   aggregate that does not exist, stop and ask. For a whole new module, run
   `pwsh build/add-module.ps1 -Name <Module> -Aggregate <Aggregate>` from the solution root instead,
   then come back here for its slices.
2. **Check the templates are installed:** `dotnet new list mmca`. If `mmca-command` and
   `mmca-query` are missing, stop and tell the user to run `dotnet new install MMCA.Templates`
   (installing a package is their call).
3. **Read the nearest existing slice** in
   `Source/Modules/<Module>/<App>.<Module>.Application/<Module>/UseCases/` and the module's
   controller. The existing code is the specification; do not invent a second way of doing this.
4. **Generate the slice** from that `UseCases` folder:
   - Command: `dotnet new mmca-command -n <UseCase> --app <App> --module <Module> --aggregate <Aggregate> --domain-method <Method>`.
     Add `--child-collection <Navigation>` only when the handler must load a child collection.
   - Query: `dotnet new mmca-query -n <UseCase> --app <App> --module <Module> --aggregate <Aggregate>`.
5. **Finish what the template printed:**
   - Command: add `<Method>` to the aggregate returning `Result`, with its rule in the aggregate's
     invariants; grow the generated validator as the command grows a payload (validators check
     input shape only); keep the command on `ICacheInvalidating<Aggregate>` with no `CachePrefix`
     of its own.
   - Query: build its `CacheKey` from `<Aggregate>CacheKeys`, adding a method there when the key
     needs one, so it stays under `<Aggregate>CacheKeys.Prefix`.
   - Both: map it in the module's controller under
     `Source/Modules/<Module>/<App>.<Module>.API/Controllers/`, following the actions already there.
     Add no DI registration: handlers are convention-scanned and the decorator pipeline wraps them.
6. **Add a test** for any new aggregate behavior in
   `Tests/Modules/<Module>/<App>.<Module>.Domain.Tests/`, beside the existing aggregate tests.
7. **Verify.** Run `dotnet build <App>.slnx` and `dotnet test --solution <App>.slnx` and show the
   summary lines. Warnings are errors, so a build reporting any warning is a failure. The
   architecture tests (validator coverage, cache invalidation, folder width, tenant scope) are part
   of that run; a red one names the rule the slice broke.

## Constraints

- Never hand-write a slice the templates generate, and never edit the generated shape into a
  different one; adapt the names and the domain call, not the structure.
- Never weaken an architecture test, an analyzer rule or an allowlist to make the slice pass.
- Never add a NuGet package, and never change the database schema without an EF migration.
- If a step conflicts with an existing convention in this solution, stop and ask rather than
  improvising.
