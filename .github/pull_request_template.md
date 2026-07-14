<!-- MMCA.Helpdesk pull request. Helpdesk is the runnable reference app for the framework. -->

## Summary

<!-- What changed and why. -->

## Checklist

- [ ] `dotnet build MMCA.Helpdesk.slnx -c Release` is clean (builds against MMCA.Common source in local-source mode; no token needed).
- [ ] `dotnet test --solution MMCA.Helpdesk.slnx` passes (domain + architecture, no database).
- [ ] If this depends on a new MMCA.Common feature, the version bump is a separate lockstep sweep PR (Helpdesk keeps no lock files).
- [ ] Commit messages follow Scoped Commits (`<scope>: <description>`).
- [ ] No secrets staged (`.pem`, `.env`, `credentials`).
