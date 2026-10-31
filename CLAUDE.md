# KleinGeld Music

## Agent skills

### Issue tracker

GitHub Issues on eldonhufkie/KleinGeldMusic, via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Starting a ticket

When `/implement` is given an issue number, branch off an up-to-date `develop` before any other work: `git switch develop && git pull --ff-only`, then `git switch -c feature/<n>-<slug>` with the slug from the issue title (e.g. `feature/6-submit-a-join-request`). Commit the ticket's work on that branch. If the working tree isn't clean or the pull isn't a fast-forward, stop and ask.

### Triage labels

The five default roles, each label named after its role (needs-triage, needs-info, ready-for-agent, ready-for-human, wontfix). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `GLOSSARY.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.
