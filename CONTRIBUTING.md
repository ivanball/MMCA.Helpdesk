# Contributing to MMCA.Helpdesk

MMCA.Helpdesk is the runnable reference app for the MMCA.Common framework: a single `Tickets`
module exercised end to end through all five layers. It is the worked companion to
`../MMCA.Common/GETTING-STARTED.md`. The full contributor reference is [CLAUDE.md](CLAUDE.md).

## Onboarding: no token needed

Helpdesk defaults to **local-source mode**: `local.props` is checked in and points at
`../MMCA.Common/Source/`, so it builds against framework source via project references with no
GitHub Packages token. Just clone the workspace alongside `MMCA.Common` and build:

```bash
dotnet build MMCA.Helpdesk.slnx -c Release
dotnet test  --solution MMCA.Helpdesk.slnx
```

The full test run (domain + architecture) needs no database and runs headless. Only the Aspire
round-trip needs SQL.

## Commit messages: Scoped Commits

This repo uses [Scoped Commits](https://scopedcommits.com/) (`<scope>: <description>`), not
Conventional Commits.

## Pull request workflow

`main` is protected. All changes land through a pull request; nobody pushes to `main` directly.

1. Branch from an up-to-date `main`, commit, push, and open a PR against `main`.
2. CI runs `build-and-test` (Release build + the headless domain/architecture tests). That is the
   required merge gate.
3. Merge once it is green. The ruleset requires **0 approving reviews today** (transitional).
   Helpdesk has no production deploy, so a merge here is not a release.

## Depending on a new MMCA.Common feature

Helpdesk keeps no `packages.lock.json` files. A framework version bump is a small
`Bump MMCA.Common to vX.Y.Z` PR cut by the maintainer via `/push-release` after the release
publishes; day-to-day, source mode already tracks Common's source.

## Branch protection (maintainer, run once)

Reproduce the ruleset with the CLI (repo admin, once). The remote is `ivanball/MMCA.Helpdesk`:

```bash
gh api -X PUT repos/ivanball/MMCA.Helpdesk/branches/main/protection \
  --input - <<'JSON'
{
  "required_status_checks": {"strict": true, "checks": [{"context": "build-and-test"}]},
  "enforce_admins": false,
  "required_pull_request_reviews": {"required_approving_review_count": 0},
  "restrictions": null,
  "required_conversation_resolution": true,
  "allow_force_pushes": false,
  "allow_deletions": false
}
JSON
```

## License

By contributing you agree your contributions are licensed under the repository's license.
