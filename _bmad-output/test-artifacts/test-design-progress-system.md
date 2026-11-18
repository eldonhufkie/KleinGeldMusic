---
runScope: 'system-level'
runKey: 'system'
workflowStatus: 'completed'
totalSteps: 5
stepsCompleted: ['step-01-detect-mode', 'step-02-load-context', 'step-03-risk-and-testability', 'step-04-coverage-plan', 'step-05-generate-output']
lastStep: 'step-05-generate-output'
nextStep: ''
lastSaved: '2026-11-16'
inputDocuments: ['GitHub issue #3', 'docs/adr/0001-audio-in-azure-blob-while-app-runs-at-home.md', 'docs/adr/0002-guid-v7-ids-in-urls.md', 'GLOSSARY.md', '.github/workflows/ci.yml', 'knowledge/adr-quality-readiness-checklist.md', 'knowledge/nfr-criteria.md', 'knowledge/test-levels-framework.md', 'knowledge/risk-governance.md', 'knowledge/test-quality.md', 'knowledge/playwright-cli.md']
---

# Test Design Progress — System Level

## Step 1: Mode

- Mode: **System-Level** (chosen by Eldon).
- PRD equivalent: GitHub issue #3 "first version" (Problem, Solution, User Stories), with build tickets as sub-issues.
- Architecture decisions: `docs/adr/0001-audio-in-azure-blob-while-app-runs-at-home.md`, `docs/adr/0002-guid-v7-ids-in-urls.md`, and research docs under `docs/research/`.
- No existing checkpoint for `system`. This is a fresh run.

## Step 2: Context

- **Stack:** backend (.NET 10, Aspire, Blazor WASM client, PostgreSQL, Azure Blob/Azurite, MailPit, FFmpeg worker). No package.json or Playwright, so browser automation is not in use; Playwright CLI fragment loaded for reference only. Contract testing not relevant (no Pact).
- **Integration points:** Azure Blob (SAS upload, SAS play links, stored-access-policy rotation, CORS), SMTP (Gmail/MailPit), FFmpeg/ffprobe, TagLib#, PostgreSQL pg_trgm/unaccent, ASP.NET Identity (cookie, security stamp, passkeys, 2FA), Data Protection keys, Tailscale Funnel, GitHub Actions + GHCR, TrueNAS compose.
- **NFRs from issue #3:** ~50 Members; revocation within ~30 s; play link ~10 min; email links 15 min; RPO 1 h, same-evening RTO; backups kept 30 days; secrets never in repo/CI; sessions survive restarts; first load ~2.2-2.5 MB accepted. No latency SLOs, no availability target (home outages accepted), no rate limit yet (#55 open).
- **Existing tests:** `KleinGeld.Api.Tests` (Aspire distributed testing, ~38 files, ~270 tests) and `KleinGeld.Web.Client.Tests` (PlayerService, ~105 tests). CI: `.github/workflows/ci.yml`.
- **Open work:** #48 deploy workflow, #49 test seed data, #55 rate-limit Join Request, #56 background email with retry, #44-46/#50-51 human-only setup.

## Step 3: Testability and Risk

### Testability concerns (actionable first)

1. **Revocation is only proven against Azurite.** Issue #3 says Azure refuses outstanding links "within about 30 seconds". Tests (`TakedownTests`, `AdminRejectAndDeleteTests`, `DeleteTests`, `LeaveTests`, `PendingChangesTests`) assert refusal against Azurite, which applies policy changes at once. Real-Azure propagation and the account-key/stored-policy setup are never exercised before Production.
2. **The Test copy has no seed yet (#49).** Issue #3: Test "starts empty plus a seed step". Until it exists, the post-deploy check on Test is manual and cannot be repeated.
3. **Emails are sent inline with no retry (#56).** Tests read MailPit, so a failing SMTP server (Gmail) is never simulated; no fault-injection seam exists for the mail sender.
4. **The UI layer has no automation by decision.** Issue #3 "Not automatically tested in v1": page rendering, Identity pages, drag-and-drop, Media Session, responsive layout. Only `PhoneLayoutTests` touch layout. Regressions there are found by hand.
5. **Deploy, rollback, backups and restore are outside the suites.** Deploy (#48) and backups/restore (#50, #51) are open; issue #3 relies on one restore rehearsal and alert checks.

### What is already strong

- **Controllability:** `TestingEndpoints` (Development + `Testing:Enabled` only) give a controllable `TimeProvider`, a clean-up trigger, catalog/Playlist seeding and a security-stamp bump. Real PostgreSQL, Azurite, MailPit and FFmpeg via `Aspire.Hosting.Testing`.
- **Observability:** behaviour is asserted at seams: HTTP status/body, MailPit messages, blob reachability. Aspire ServiceDefaults supply OpenTelemetry.
- **Reliability:** `PlayerService` sits behind a narrow JS/storage interface with a fake (~105 tests). `PlayLinks` is a singleton with a semaphore, so rotation is serialized as issue #3 requires.
- **Headless:** all business rules are reachable through the HTTP API; the client holds only Queue logic, which has its own seam.

### ASRs

| ASR | Source | Status |
|-----|--------|--------|
| Play links ~10 min, revoked within ~30 s on Remove/Leave/Takedown/Reject/delete/approved replace | Issue #3, ADR 0001 | ACTIONABLE (prove on real Azure once) |
| One visibility rule: unpublished content answers "not found" except to managers and Admin | Issue #3 | ACTIONABLE (endpoint x actor matrix) |
| GUID v7 ids in URLs; slug corrected by redirect | ADR 0002 | FYI (covered) |
| Security stamp validated on every request | Issue #3 | FYI (covered) |
| Blob names write-once | Issue #3 | FYI (covered) |
| Migrator runs once before the API starts | Issue #3 | ACTIONABLE (#48 deploy smoke) |
| RPO 1 h, same-evening restore, copies kept 30 days | Issue #3 | ACTIONABLE (rehearsal #51) |
| Secrets never in repo or CI | Issue #3 | FYI (push protection + review) |

### Risk register

| ID | Cat | Risk (with source) | P | I | Score | Mitigation | Owner | When |
|----|-----|--------------------|---|---|-------|------------|-------|------|
| R-01 | SEC | A Removed or Left Member, or a Taken-down Release, stays playable because policy rotation fails or propagates slower than "about 30 seconds" on real Azure (issue #3, ADR 0001) | 2 | 3 | **6** | Keep Azurite revocation tests; add one manual check on Test against real Azure after #48; log rotation failures | Eldon | Before launch |
| R-02 | SEC | An endpoint skips the visibility rule and leaks a Draft/Submitted/Taken-down Release, or Pending Changes, to Members (issue #3 "applied in one place"; story 85) | 2 | 3 | **6** | Table-driven test: every read endpoint x actor (Member, manager, Admin) x stage | Eldon/agent | Next sprint |
| R-03 | DATA | Leave/Remove deletes the wrong Artists or data, e.g. a band Artist with other managers (stories 118, 120, 124) | 2 | 3 | **6** | Cover solely- vs jointly-managed Artists, Join Request deletion, blobs gone; preview equals what is deleted | Eldon/agent | Next sprint |
| R-04 | DATA | Pending Changes approve/fold/reject corrupts a Release or breaks Playlist entries (replace must keep the Track) (stories 53-58, 56) | 2 | 3 | **6** | Extend `PendingChangesTests` for fold on Takedown and on Admin Reject, and Playlist entry survival | Eldon/agent | Next sprint |
| R-05 | OPS | Backups or restore fail when needed; at most 1 h loss not met (stories 133-135; #50, #51 open) | 2 | 3 | **6** | Restore rehearsal into `kleingeld-test`; TrueNAS failure alerts tested once | Eldon | Before launch |
| R-06 | SEC | Join Request form abused for spam or flooding (no rate limit; #55) | 2 | 2 | 4 | #55 with tests for 429 and unchanged non-enumerating response | agent | Before launch |
| R-07 | OPS | An email (setup link, rejection reason) is silently lost when Gmail SMTP fails (#56) | 2 | 2 | 4 | #56 background send with retry; fault-injection test with a failing SMTP double | agent | Before launch |
| R-08 | OPS | Deploy publishes a broken release: migrator order, wrong tag, failed rollback (stories 128-131; #48) | 2 | 2 | 4 | Post-deploy smoke on Test (login, browse, play) using #49 seed; rehearse one rollback | Eldon | With #48 |
| R-09 | TECH | Conversion behaves differently in the FFmpeg base image than on CI's apt FFmpeg (ci.yml) | 2 | 2 | 4 | Run conversion fixtures once in the published image on Test | Eldon | With #48 |
| R-10 | TECH | UI regressions (pages, drag-and-drop, Media Session, phone layout) go unnoticed (issue #3 "Not automatically tested") | 2 | 2 | 4 | Manual exploratory checklist per release on Test | Eldon | Each release |
| R-11 | TECH | Test suite gets slow or flaky (containers, real FFmpeg, 30-min CI timeout) | 2 | 2 | 4 | Track duration; poll, never sleep; parallel fixtures where safe | agent | Ongoing |
| R-12 | SEC | A secret lands in the repo or CI (story 132) | 1 | 3 | 3 | GitHub push protection; secrets only in `secrets.env` | Eldon | Done/ongoing |
| R-13 | SEC | Removed Member keeps a session (story 123) | 1 | 3 | 3 | Covered by `RemoveMemberTests` and stamp check | — | Covered |
| R-14 | SEC | Admin or another Member can see a private Playlist (story 116) | 1 | 3 | 3 | Covered by Playlist tests; include in R-02 matrix | — | Covered |
| R-15 | BUS | Player Queue rules regress (stories 86-95) | 1 | 2 | 2 | Covered by `PlayerServiceTests` | — | Covered |
| R-16 | PERF | Conversion time on TrueNAS unknown (issue #3 accepted risk) | 2 | 1 | 2 | Measure once deployed | Eldon | After launch |

High risks (score >= 6): R-01 to R-05. No score 9, so nothing blocks by rule.

### NFR planning

| Category | Threshold (source) | Evidence planned |
|----------|-------------------|------------------|
| Security | Revocation <= ~30 s; links ~10 min; email links 15 min; stamp checked every request; non-enumerating Join Request; Playlists private | API tests (exist); one real-Azure check (R-01) |
| Reliability | Sessions survive restarts (Data Protection keys on volume); player refetches link on failure | `DataProtectionTests`, `PlayerServiceTests` |
| Disaster recovery | RPO 1 h; same-evening RTO; copies 30 days | Restore rehearsal (#51), alert checks (#50) |
| Deployability | Migrator before API; same image tag Test to Prod; rollback by tag | #48 smoke on Test |
| Performance | **UNKNOWN**: page/API latency, upload throughput, conversion time | Clarify; measure after launch (R-16) |
| Availability | **UNKNOWN by design**: home outages accepted | None planned |
| Throttling | **UNKNOWN**: Join Request limit value (#55) | Decide in #55 |

Clarifications for Eldon: latency target (if any); Join Request rate-limit value; whether a single real-Azure revocation check on Test is acceptable as the R-01 evidence.

### Summary

The design is very testable: real dependencies, a controllable clock, test-only seeding, and two clean seams. The highest risks are not in untested features but in the places tests cannot reach: real Azure revocation timing (R-01), the deploy/backup path (R-05, R-08), and gaps where one rule must hold across many endpoints (R-02 visibility, R-03 deletion, R-04 Pending Changes).

## Step 4: Coverage Plan

Levels follow the project's two seams: **API** = `KleinGeld.Api.Tests` (Aspire, real dependencies); **Unit** = `KleinGeld.Web.Client.Tests` (`PlayerService` behind a fake). **Manual** = a scripted check on the Test copy, used only where issue #3 rules out automation or a real external service is the point. No E2E browser suite in v1 (issue #3 decision).

### Coverage matrix

| ID | Scenario | Level | Pri | Risk Link | Status |
|----|----------|-------|-----|-----------|--------|
| C-01 | Link issued before Takedown / Admin Reject / delete / Leave / Remove / approved replace is refused by Blob; fresh link for another Published Track works | API | P0 | R-01 | Exists (6 files) |
| C-02 | Two concurrent revocations leave exactly one live policy and links still issue | API | P1 | R-01 | **New** |
| C-03 | On Test (real Azure): link refused within ~30 s after a Takedown | Manual | P0 | R-01 | **New** (needs #48) — Azurite cannot show propagation |
| C-04 | Every Member-facing read (Release, Artist, Track play link, search, lists, home, Playlist entries) x actor (Member / manager / Admin) x stage (Draft, Submitted, Rejected, Taken down, Pending Changes) answers per the visibility rule | API | P0 | R-02, R-14 | **New** (table-driven; parts exist scattered) |
| C-05 | Leave with a solely-managed and a jointly-managed Artist: sole Artist, Releases and blobs gone; joint Artist stays with other manager; Join Request and Playlists gone | API | P0 | R-03 | Partly exists (`LeaveTests`) — add joint-Artist case |
| C-06 | Remove: same deletion as Leave; preview equals what is deleted; email 10 lists deleted Artists; no email 9 | API | P0 | R-03 | Exists (`RemoveMemberTests`) — add preview == result |
| C-07 | Pending Changes fold into the Release on Takedown and on Admin Reject; resubmission reviews the whole | API | P0 | R-04 | **New** / verify in `PendingChangesTests` |
| C-08 | Replaced Track keeps its id, stays on Playlists, plays old audio until approval and new after | API | P0 | R-04 | Exists — confirm Playlist leg |
| C-09 | Restore rehearsal into `kleingeld-test` from off-site dump; spot-check login, browse, play | Manual | P0 | R-05 | **New** (#51) |
| C-10 | Each backup task failing raises a TrueNAS email | Manual | P1 | R-05 | **New** (#50) |
| C-11 | Join Request over the limit gets 429; under the limit the response stays identical for new and existing emails | API | P1 | R-06 | **New** (#55) |
| C-12 | SMTP down at send time: request still succeeds; email delivered after recovery | API | P1 | R-07 | **New** (#56; needs a failing-SMTP double or stopping MailPit) |
| C-13 | Post-deploy smoke on Test with seed: migrator completed, login, browse, play, upload converts | Manual (scriptable) | P1 | R-08, R-09 | **New** (#48, #49) |
| C-14 | Rollback by changing tag in `release.env` restores previous version | Manual | P2 | R-08 | **New** |
| C-15 | Exploratory checklist: pages, drag-and-drop reorder, Media Session, phone layout, loading screen | Manual | P2 | R-10 | **New** |
| C-16 | Suite duration and flake tracking in CI | CI | P2 | R-11 | **New** |
| C-17 | Join Request -> approve -> setup link -> password -> login; 15-min expiry | API | P0 | R-13 | Exists |
| C-18 | Removed Member refused on next request and on play link | API | P0 | R-13 | Exists |
| C-19 | Release stages and forbidden transitions; Rights Declaration recorded | API | P0 | — (core flow) | Exists |
| C-20 | Upload validation, conversion Ready/Failed, write-once blob names, 24 h clean-up | API | P1 | R-09 | Exists |
| C-21 | Search (case, accents, prefix at 2, trigram at 3), paging 48 | API | P1 | — | Exists |
| C-22 | Playlist rules: duplicates, limits, Undo, Unavailable snapshot and revival | API | P1 | R-14 | Exists |
| C-23 | Ten emails to the right people, Admin own-Artist exceptions | API | P1 | R-07 | Exists |
| C-24 | Queue, shuffle, repeat, Previous 3 s, link refetch, skip on refusal, persistence | Unit | P1 | R-15 | Exists (98 tests) |
| C-25 | Conversion time measured on TrueNAS | Manual | P3 | R-16 | **New** (after launch) |
| C-26 | No secrets in repo: push protection on; `secrets.env` only on server | CI/config | P1 | R-12 | Exists (setup) |

No scenario is duplicated across levels: API owns rules, Unit owns client Queue logic, Manual owns what only the real host or Azure can show.

### NFR evidence plan

| NFR | Scenarios | Evidence for `nfr-assess` |
|-----|-----------|---------------------------|
| Security | C-01..C-04, C-11, C-17, C-18, C-26 | CI test results; C-03 note with timing |
| Reliability | C-12, C-24, `DataProtectionTests` | CI test results |
| Disaster recovery | C-09, C-10 | Rehearsal log with times; alert email screenshots |
| Deployability | C-13, C-14 | Deploy workflow run + smoke log |
| Performance | C-25 | **Blocked on UNKNOWN thresholds**; measurement only |
| Maintainability | C-16 | CI duration trend |

### Execution strategy

- **PR / every push:** both suites, everything automated (current CI, 30-min timeout). Keep under ~15 min; if it grows past that, split slow conversion/revocation tests into a nightly job.
- **Each deploy to Test:** C-13 smoke; C-03 at least once and after any storage change.
- **Before launch, once:** C-09, C-10, C-14, C-15.
- **After launch:** C-25; repeat C-09 only after major PostgreSQL/TrueNAS upgrades (issue #3).

### Estimates (new work only)

- P0: ~10-18 h (C-04 matrix is most of it; C-05, C-07 additions; C-03, C-09 manual)
- P1: ~8-14 h (C-02, C-11, C-12, C-13, C-10)
- P2: ~3-6 h (C-14, C-15, C-16)
- P3: ~1 h
- **Total ~22-39 h**, about 1-2 weeks of evenings, mostly riding along with #48, #49, #50, #51, #55, #56.

### Quality gates

- P0 pass rate 100%; P1 >= 95% (no known P1 failure without a ticket).
- R-01 to R-05 mitigated (C-01..C-09 done) before Production launch.
- No line-coverage percentage gate: behaviour coverage via the matrix instead, matching issue #3's "test through seams" rule. (Deviation from the default 80% target, justified by that decision.)
- Each NFR row has its evidence source named; PASS/CONCERNS/FAIL left to `nfr-assess`.

## Step 5: Outputs

- Mode: system-level, sequential generation.
- `_bmad-output/test-artifacts/test-design-architecture.md`
- `_bmad-output/test-artifacts/test-design-qa.md`
- `_bmad-output/test-artifacts/test-design/KleinGeldMusic-handoff.md`
- Validated against `checklist.md`. Deliberate deviations: no playwright-utils example or Playwright/k6 tiers (.NET stack, no performance thresholds); no line-coverage gate (issue #3 tests through seams); test additions in the architecture mitigation plans because Eldon is both developer and tester.
