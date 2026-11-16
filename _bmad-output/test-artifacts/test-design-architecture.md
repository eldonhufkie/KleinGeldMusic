---
workflowStatus: 'completed'
totalSteps: 5
stepsCompleted: ['step-01-detect-mode', 'step-02-load-context', 'step-03-risk-and-testability', 'step-04-coverage-plan', 'step-05-generate-output']
lastStep: 'step-05-generate-output'
nextStep: ''
lastSaved: '2026-11-16'
workflowType: 'testarch-test-design'
inputDocuments: ['GitHub issue #3', 'docs/adr/0001-audio-in-azure-blob-while-app-runs-at-home.md', 'docs/adr/0002-guid-v7-ids-in-urls.md', 'GLOSSARY.md', '.github/workflows/ci.yml', 'src/KleinGeld.Web/Blobs/PlayLinks.cs', 'src/KleinGeld.Web/Testing/TestingEndpoints.cs', 'tests/']
---

# Test Design for Architecture: KleinGeld Music first version

**Purpose:** Architectural concerns, testability gaps and NFR requirements that must be settled before launch, so the remaining test work has something solid to stand on.

**Date:** 2026-11-16
**Author:** Murat (TEA) for Eldon Hufkie
**Status:** Architecture Review Pending
**Project:** KleinGeldMusic
**PRD Reference:** GitHub issue #3 "first version"
**ADR Reference:** [ADR 0001](../../docs/adr/0001-audio-in-azure-blob-while-app-runs-at-home.md), [ADR 0002](../../docs/adr/0002-guid-v7-ids-in-urls.md)

---

## Executive Summary

**Scope:** The whole first version: Join Requests and accounts, Artists, the Release pipeline (upload, conversion, review, Pending Changes, Takedown), browsing and search, the player, Playlists, Leave/Remove, and running it on TrueNAS with audio in Azure Blob.

**Business context** (issue #3):

- **Impact:** Private platform for ~50 musicians and friends. No payments. Trust rests on the Admin controlling who joins and what gets published.
- **Problem:** Public services are open to everyone and give the group no control; sharing files by hand loses browsing, a continuous player and Playlists.
- **Launch:** After deploy (#48), seed (#49), backups (#50) and restore rehearsal (#51). Most build tickets are closed.

**Architecture:**

- **Audio in Azure Blob, app at home** (ADR 0001): SAS upload and ~10-minute SAS play links signed against a stored access policy that is rotated to revoke access.
- **GUID v7 ids in URLs** (ADR 0002): unguessable ids protect unpublished content; slugs corrected by redirect.
- **Stack:** .NET 10, Aspire, Blazor WebAssembly (no prerendering), ASP.NET Identity with cookie and security stamp checked on every request, PostgreSQL with `pg_trgm`/`unaccent`, FFmpeg worker, MailKit SMTP.

**Expected scale:** ~50 Members, a few thousand Tracks, one home server, one PostgreSQL.

**Risk summary:**

- **Total risks:** 16
- **High priority (>= 6):** 5 (R-01 to R-05). None scores 9.
- **Test effort:** 26 scenarios in the coverage plan, 11 already covered by ~375 existing tests; new work ~22-39 hours.

---

## Quick Guide

### 🚨 BLOCKERS: must be in place before the remaining tests can run

1. **#48 Deploy workflow**: C-03 (real-Azure revocation) and C-13 (post-deploy smoke) need a running Test copy. (Owner: Eldon/agent)
2. **#49 Test seed data**: the smoke check needs a known Admin, Members and Releases on Test. (Owner: agent)
3. **#50/#51 Backups and restore**: C-09 and C-10 are the only evidence for the 1-hour RPO and same-evening restore. (Owner: Eldon)

**What's needed:** finish these four tickets before launch; the API suite itself is not blocked.

### ⚠️ HIGH PRIORITY: recommendation for Eldon to approve

1. **R-01 Revocation on real Azure:** accept one manual timed check on Test as the evidence, because Azurite applies policy changes instantly and can't show the ~30 s propagation.
2. **R-02 Visibility rule:** add one table-driven API test across every Member-facing read, actor and stage, rather than relying on per-feature tests.
3. **R-03/R-04 Data integrity:** add the jointly-managed-Artist Leave case and Pending Changes fold on Takedown/Admin Reject.

### 📋 INFO ONLY

1. **Test strategy:** API level (Aspire, real dependencies) for all rules; Unit level for `PlayerService`; Manual on Test only where a real host or Azure is the point. No browser E2E in v1 (issue #3 decision).
2. **Tooling:** xUnit, `Aspire.Hosting.Testing`, Azurite, MailPit API, FFmpeg, controllable `TimeProvider` via `/testing/clock`.
3. **CI:** both suites on every push and PR (`ci.yml`, 30-minute limit).
4. **Coverage:** 26 scenarios P0-P3 (see `test-design-qa.md`).
5. **Quality gates:** P0 100%, P1 >= 95%, R-01 to R-05 mitigated before Production.
6. **Already-met ASRs:** GUID v7 ids with slug redirect (ADR 0002), security stamp on every request, write-once blob names, secrets only on the server. All are covered by existing tests or setup.

---

## For Architects and Devs: Open Topics 👷

### Risk Assessment

**Total risks identified:** 16 (5 high, 7 medium, 4 low)

#### High-Priority Risks (Score >= 6)

| Risk ID | Category | Description | P | I | Score | Mitigation | Owner | Timeline |
|---------|----------|-------------|---|---|-------|------------|-------|----------|
| **R-01** | **SEC** | A Removed or Left Member, or a Taken-down Release, stays playable because policy rotation fails or real Azure takes longer than "about 30 seconds" (issue #3, ADR 0001) | 2 | 3 | **6** | Keep Azurite revocation tests; add a rotation concurrency test; one timed check on Test | Eldon | Before launch |
| **R-02** | **SEC** | An endpoint skips the single visibility rule and leaks unpublished content or Pending Changes (issue #3; story 85) | 2 | 3 | **6** | Table-driven endpoint x actor x stage API test | Eldon/agent | Next sprint |
| **R-03** | **DATA** | Leave/Remove deletes the wrong Artists or data, e.g. a band Artist with other managers (stories 118, 120, 124) | 2 | 3 | **6** | Joint-Artist case; preview equals result | Eldon/agent | Next sprint |
| **R-04** | **DATA** | Pending Changes approve/fold/reject corrupts a Release or breaks Playlist entries (stories 53-58) | 2 | 3 | **6** | Fold on Takedown and Admin Reject; replaced Track stays on Playlists | Eldon/agent | Next sprint |
| **R-05** | **OPS** | Backups or restore fail when needed; 1-hour RPO not met (stories 133-135; #50, #51) | 2 | 3 | **6** | Restore rehearsal; alert check | Eldon | Before launch |

#### Medium-Priority Risks (Score 3-5)

| Risk ID | Category | Description | P | I | Score | Mitigation | Owner |
|---------|----------|-------------|---|---|-------|------------|-------|
| R-06 | SEC | Join Request form flooded; no rate limit yet (#55) | 2 | 2 | 4 | #55 with 429 tests, non-enumerating response kept | agent |
| R-07 | OPS | Email lost when Gmail SMTP fails; sent inline, no retry (#56) | 2 | 2 | 4 | #56 background send with retry; failing-SMTP test | agent |
| R-08 | OPS | Broken release deployed: migrator order, wrong tag, failed rollback (#48) | 2 | 2 | 4 | Post-deploy smoke on Test; one rollback rehearsal | Eldon |
| R-09 | TECH | Conversion differs between CI's apt FFmpeg and the published base image | 2 | 2 | 4 | Convert a fixture in the published image on Test | Eldon |
| R-10 | TECH | UI regressions unnoticed (pages, drag-and-drop, Media Session, phone layout; not automated by decision) | 2 | 2 | 4 | Manual exploratory checklist per release | Eldon |
| R-11 | TECH | Test suite slow or flaky (containers, real FFmpeg, 30-min CI limit) | 2 | 2 | 4 | Track duration; poll, never sleep | agent |
| R-12 | SEC | A secret lands in the repo or CI (story 132) | 1 | 3 | 3 | Push protection; secrets only in `secrets.env` | Eldon |
| R-13 | SEC | Removed Member keeps a session (story 123) | 1 | 3 | 3 | Covered by `RemoveMemberTests` | — |
| R-14 | SEC | Someone else, the Admin included, sees a private Playlist (story 116) | 1 | 3 | 3 | Covered; include in R-02 matrix | — |

#### Low-Priority Risks (Score 1-2)

| Risk ID | Category | Description | P | I | Score | Action |
|---------|----------|-------------|---|---|-------|--------|
| R-15 | BUS | Player Queue rules regress (stories 86-95) | 1 | 2 | 2 | Document; covered by ~98 `PlayerService` tests |
| R-16 | PERF | Conversion time on TrueNAS unknown (accepted risk in issue #3) | 2 | 1 | 2 | Document; measure after launch |

#### Risk Category Legend

- **TECH**: technical/architecture
- **SEC**: security, access, data exposure
- **PERF**: performance
- **DATA**: data integrity
- **BUS**: business logic and user experience
- **OPS**: deployment, configuration, backups

---

### NFR Testability Requirements

| NFR Category | Threshold / Requirement | Current Design Support | Gap / Decision Needed | Planned Evidence |
|--------------|------------------------|------------------------|-----------------------|------------------|
| Security | Revocation within ~30 s; play links ~10 min; email links 15 min; stamp checked every request; non-enumerating Join Request; private Playlists; secrets off repo/CI | Supported; tested against Azurite | Real-Azure timing unproven (R-01); rate limit missing (#55) | API test results; timed Test check |
| Performance | **UNKNOWN**: no latency, upload or conversion targets | Unknown | Decide whether any target matters at ~50 Members | Measurement after launch (C-25) |
| Reliability | Sessions survive restarts; player refetches a failed link; emails delivered | Partial: Data Protection keys on a volume; link refetch tested; email has no retry | #56 | `DataProtectionTests`, `PlayerServiceTests`, #56 tests |
| Disaster recovery | RPO 1 h; same-evening RTO; copies kept 30 days | Designed (snapshots, hourly dump, Cloud Sync) | Not yet built or rehearsed (#50, #51) | Rehearsal log, alert emails |
| Deployability | Migrator before API; same image tag Test to Production; rollback by tag | Designed; images and compose published by CI | Deploy job (#48) open | Workflow run + smoke log |
| Maintainability | Tests through seams; CI on every push | Supported | No duration tracking | CI timing |
| Availability | None by design (home outages accepted) | — | — | None |

**Unknown thresholds:** API/page latency; upload throughput; conversion time; Join Request rate-limit value (#55).

**Assessment boundary:** final PASS/CONCERNS/FAIL belongs in `nfr-assess` once evidence exists.

---

### Testability Concerns and Architectural Gaps

#### 1. Blockers to Fast Feedback

| Concern | Impact | What Architecture Must Provide | Owner | Timeline |
|---------|--------|-------------------------------|-------|----------|
| **No real-Azure test path** | Revocation timing (R-01) and CORS for the `ts.net` origin only proven against Azurite | A running Test copy with its own Azure container (#48) | Eldon | Before launch |
| **No seed for Test** | Post-deploy smoke can't be repeated | Seed step (#49) | agent | With #48 |
| **No SMTP fault seam** | Can't show an email survives a mail outage (R-07) | A send queue with retry (#56), testable by stopping MailPit or a failing double | agent | Before launch |

#### 2. Architectural Improvements Needed

1. **Log policy rotation outcomes**
   - **Current problem:** if `RevokeAllAsync` throws, the triggering action may succeed while old links stay valid, and nothing records it.
   - **Required change:** log rotation success/failure with the triggering event; surface failures.
   - **Impact if not fixed:** a silent revocation failure keeps a Removed Member listening for up to 10 minutes.
   - **Owner:** agent. **Timeline:** before launch.

---

### Testability Assessment Summary

#### What Works Well

- ✅ Real dependencies in tests: PostgreSQL, Azurite, MailPit and FFmpeg through `Aspire.Hosting.Testing`.
- ✅ Test-only hooks (`/testing/*`, Development + `Testing:Enabled` only): controllable clock, clean-up trigger, catalog seeding, security-stamp bump.
- ✅ Every business rule is reachable through the HTTP API; client logic sits behind its own seam with a fake.
- ✅ `PlayLinks` is a singleton with a semaphore, so rotation is serialized as issue #3 requires.
- ✅ Behaviour assertions on observable results (status, email, blob reachability), not internals.

#### Accepted Trade-offs (No Action Required)

- **No browser E2E in v1:** pages, Identity templates, drag-and-drop, Media Session and layout are checked by hand (issue #3).
- **No availability target:** home power and ISP outages are accepted.
- **No automated restore tests:** one rehearsal before launch and after major upgrades.

These are accepted for the first version; revisit if Members grow into the hundreds.

---

### Risk Mitigation Plans (Score >= 6)

#### R-01: Revocation fails or is slow on real Azure (Score 6) - HIGH

1. Keep the existing Azurite tests (Takedown, Admin Reject, delete, Leave, Remove, approved replace).
2. Add C-02: two concurrent revocations leave one live policy and links still issue.
3. After #48, on Test: issue a link, take the Release down, poll the link until refused; record the time.
4. Log rotation success/failure.

**Owner:** Eldon. **Timeline:** before launch. **Status:** In progress.
**Verification:** C-01 green in CI; C-03 refused within ~30 s.

#### R-02: Visibility rule bypassed (Score 6) - HIGH

1. Build a table-driven test listing every Member-facing read endpoint.
2. For each stage (Draft, Submitted, Rejected, Taken down, Pending Changes), assert: Member gets "not found"; manager and Admin see it; Playlists stay private.
3. Add new endpoints to the table as they appear.

**Owner:** Eldon/agent. **Timeline:** next sprint. **Status:** Planned.
**Verification:** C-04 green; a deliberately broken endpoint makes it fail.

#### R-03: Leave/Remove deletes the wrong data (Score 6) - HIGH

1. Add a Leave case with one solely-managed and one jointly-managed Artist.
2. Assert the preview lists exactly what is then deleted.
3. Assert blobs of deleted Releases are gone and the joint Artist keeps its Releases.

**Owner:** Eldon/agent. **Timeline:** next sprint. **Status:** Planned.
**Verification:** C-05 and C-06 green.

#### R-04: Pending Changes corrupt a Release or Playlists (Score 6) - HIGH

1. Add fold-on-Takedown and fold-on-Admin-Reject cases; resubmission is reviewed whole.
2. Confirm a replaced Track keeps its id and Playlist entry, old audio until approval, new after.

**Owner:** Eldon/agent. **Timeline:** next sprint. **Status:** Planned.
**Verification:** C-07 and C-08 green.

#### R-05: Backups or restore fail (Score 6) - HIGH

1. Build backups per issue #3 (#50).
2. Trigger one failure of each task and confirm the alert email.
3. Rehearse restore into `kleingeld-test` from an off-site dump (#51); time it; spot-check login, browse, play.

**Owner:** Eldon. **Timeline:** before launch. **Status:** Planned.
**Verification:** rehearsal log shows restore the same evening and data no older than 1 hour.

---

### Assumptions and Dependencies

#### Assumptions

1. Scale stays around 50 Members, so no load testing is planned.
2. Azurite behaves like Azure for SAS and stored policies, except for propagation delay.
3. Eldon is the only developer; "owner" means Eldon or an agent working for Eldon.

#### Dependencies

1. #48 Deploy workflow: required before C-03, C-13, C-14.
2. #49 Test seed: required before C-13.
3. #50, #51 Backups and rehearsal: required before launch.
4. #55, #56: required before C-11, C-12.

#### Risks to Plan

- **Risk:** CI time grows past the 30-minute limit as tests are added.
  - **Impact:** slower feedback, timeouts.
  - **Contingency:** move conversion and revocation tests to a nightly job.

---

**End of Architecture Document.**

**Next steps:** finish #48, #49, #50, #51; approve the R-01 evidence approach; see `test-design-qa.md` for scenarios.
