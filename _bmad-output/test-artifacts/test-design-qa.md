---
workflowStatus: 'completed'
totalSteps: 5
stepsCompleted: ['step-01-detect-mode', 'step-02-load-context', 'step-03-risk-and-testability', 'step-04-coverage-plan', 'step-05-generate-output']
lastStep: 'step-05-generate-output'
nextStep: ''
lastSaved: '2026-11-16'
workflowType: 'testarch-test-design'
inputDocuments: ['GitHub issue #3', 'docs/adr/0001-audio-in-azure-blob-while-app-runs-at-home.md', 'docs/adr/0002-guid-v7-ids-in-urls.md', '.github/workflows/ci.yml', 'tests/']
---

# Test Design for QA: KleinGeld Music first version

**Purpose:** What to test, at which level, and what is still needed before launch.

**Date:** 2026-11-16
**Author:** Murat (TEA) for Eldon Hufkie
**Status:** Draft
**Project:** KleinGeldMusic

**Related:** `test-design-architecture.md` for risks, blockers and mitigation plans.

---

## Executive Summary

**Scope:** The first version as described in issue #3, tested through its two seams (HTTP API, client `PlayerService`), plus manual checks on the Test copy where only the real host or Azure can show the result.

**Risk summary:**

- Total risks: 16 (5 high, 7 medium, 4 low)
- Critical categories: SEC (R-01, R-02) and DATA (R-03, R-04), plus OPS (R-05)

**Coverage summary:**

- P0 scenarios: 11 (5 exist, 6 new or extended)
- P1 scenarios: 11 (6 exist, 5 new)
- P2 scenarios: 3 (new)
- P3 scenarios: 1 (new)
- Existing automated tests: ~270 API + ~105 client
- **New work:** ~22-39 hours

---

## Not in Scope

| Item | Reasoning | Mitigation |
|------|-----------|------------|
| **Browser E2E (pages, Identity templates, drag-and-drop, Media Session, layout)** | Issue #3: not automatically tested in v1 | C-15 manual checklist per release |
| **Load and performance testing** | ~50 Members; no thresholds | C-25 measurement after launch |
| **Availability and failover** | Home outages accepted (issue #3) | None |
| **Automated restore tests, WAL archiving** | Out of scope in issue #3 | C-09 rehearsal |
| **Contract testing** | One client, same repository | API tests cover the contract |

---

## Dependencies & Test Blockers

### Backend/Architecture Dependencies

1. **#48 Deploy workflow**: Eldon/agent, before launch
   - A running Test copy with its own Azure container.
   - Needed for C-03, C-13, C-14.
2. **#49 Test seed data**: agent, with #48
   - Known Admin, Members and Releases on Test.
   - Needed for C-13.
3. **#55 Rate limit** and **#56 Background email**: agent, before launch
   - Needed for C-11, C-12.
4. **#50 Backups**, **#51 Restore rehearsal**: Eldon, before launch
   - Needed for C-09, C-10.

### Test Infrastructure (in place)

1. **Test data**: `/testing/*` endpoints seed catalog, Releases, Playlist filler, and bump security stamps; `NewMembers` creates actors through the real Join Request flow.
2. **Environments**:
   - Local: `dotnet test` starts the AppHost with PostgreSQL, Azurite, MailPit; FFmpeg on PATH.
   - CI: `ci.yml` on ubuntu-latest with FFmpeg, Aspire CLI and trusted dev certificate.
   - Test copy: `kleingeld-test` on TrueNAS, tailnet-only (after #48).

**Example (API seam).** The template's playwright-utils example doesn't apply: this is a .NET solution tested with xUnit and Aspire, with no Playwright suite.

```csharp
[Fact]
public async Task A_link_issued_before_Takedown_stops_working()
{
    var (manager, release) = await Releases.PublishedWithOneTrackAsync();
    var link = await manager.PlayLinkAsync(release.Tracks[0].Id);

    await manager.TakeDownAsync(release.Id);

    using var response = await BlobStorage.GetAsync(link.Url);
    Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
}
```

---

## Risk Assessment

### High-Priority Risks (Score >= 6)

| Risk ID | Category | Description | Score | QA Test Coverage |
|---------|----------|-------------|-------|------------------|
| **R-01** | SEC | Revocation fails or is slow on real Azure | **6** | C-01 (exists), C-02, C-03 |
| **R-02** | SEC | Visibility rule bypassed | **6** | C-04 |
| **R-03** | DATA | Leave/Remove deletes the wrong data | **6** | C-05, C-06 |
| **R-04** | DATA | Pending Changes corrupt a Release or Playlists | **6** | C-07, C-08 |
| **R-05** | OPS | Backups or restore fail | **6** | C-09, C-10 |

### Medium/Low-Priority Risks

| Risk ID | Category | Description | Score | QA Test Coverage |
|---------|----------|-------------|-------|------------------|
| R-06 | SEC | Join Request flooding | 4 | C-11 |
| R-07 | OPS | Email lost on SMTP failure | 4 | C-12, C-23 |
| R-08 | OPS | Broken deploy or rollback | 4 | C-13, C-14 |
| R-09 | TECH | FFmpeg differs in published image | 4 | C-13, C-20 |
| R-10 | TECH | UI regressions | 4 | C-15 |
| R-11 | TECH | Slow or flaky suite | 4 | C-16 |
| R-12 | SEC | Secret in repo or CI | 3 | C-26 |
| R-13 | SEC | Removed Member keeps session | 3 | C-17, C-18 |
| R-14 | SEC | Private Playlist exposed | 3 | C-04, C-22 |
| R-15 | BUS | Queue rules regress | 2 | C-24 |
| R-16 | PERF | Conversion time unknown | 2 | C-25 |

---

## NFR Test Coverage Plan

| NFR Category | Requirement / Threshold | Planned Validation | Tool / Level | Evidence Artifact | Priority |
|--------------|------------------------|--------------------|--------------|-------------------|----------|
| Security | Revocation ~30 s; visibility; stamp; non-enumeration; rate limit | C-01..C-04, C-11, C-17, C-18 | API (xUnit + Aspire); Manual on Test | CI test results; C-03 timing note | P0 |
| Reliability | Sessions survive restarts; link refetch; email retry | `DataProtectionTests`, C-24, C-12 | API, Unit | CI test results | P1 |
| Disaster recovery | RPO 1 h; same-evening RTO | C-09, C-10 | Manual | Rehearsal log; alert emails | P0 |
| Deployability | Migrator first; same tag; rollback | C-13, C-14 | Manual (scriptable) | Workflow run; smoke log | P1 |
| Performance | **UNKNOWN** | C-25 | Manual measurement | Timing notes | P3 |
| Maintainability | Suite stays within CI limit | C-16 | CI | Duration trend | P2 |

**Missing thresholds:** latency, upload throughput, conversion time, Join Request rate-limit value.

---

## Entry Criteria

- [x] Requirements agreed (issue #3)
- [x] Local and CI test environments working
- [x] Seed hooks available for API tests
- [ ] Test copy deployed (#48) and seeded (#49): for Manual checks only

## Exit Criteria (before Production launch)

- [ ] All P0 scenarios passing, including C-03 and C-09
- [ ] P1 >= 95% passing; any failure has a ticket
- [ ] R-01 to R-05 mitigated
- [ ] C-15 exploratory checklist done on Test
- [ ] Unknown performance thresholds either set or explicitly accepted

---

## Test Coverage Plan

P0-P3 are priority, not timing.

### P0 (Critical)

| Test ID | Requirement | Test Level | Risk Link | Notes |
|---------|-------------|------------|-----------|-------|
| **C-01** | Link issued before Takedown / Admin Reject / delete / Leave / Remove / approved replace is refused; fresh link for another Published Track works | API | R-01 | Exists |
| **C-03** | On Test, link refused within ~30 s after a Takedown | Manual | R-01 | New; Azurite can't show propagation |
| **C-04** | Every Member-facing read x actor x stage follows the visibility rule; Playlists private | API | R-02, R-14 | New, table-driven |
| **C-05** | Leave with sole and joint Artists deletes exactly the right data and blobs | API | R-03 | Extend `LeaveTests` |
| **C-06** | Remove: same deletion; preview equals result; email 10 only | API | R-03 | Extend `RemoveMemberTests` |
| **C-07** | Pending Changes fold on Takedown and Admin Reject; resubmission reviewed whole | API | R-04 | New / verify |
| **C-08** | Replaced Track keeps id and Playlist entry; old audio until approval | API | R-04 | Exists; confirm Playlist leg |
| **C-09** | Restore rehearsal into `kleingeld-test` | Manual | R-05 | New (#51) |
| **C-17** | Join Request -> approve -> setup link -> login; 15-min expiry | API | R-13 | Exists |
| **C-18** | Removed Member refused at once, including play link | API | R-13 | Exists |
| **C-19** | Release stages, forbidden transitions, Rights Declaration recorded | API | — | Exists; core flow |

**Total P0:** 11 scenarios (C-19 is a core flow without a scored risk)

### P1 (High)

| Test ID | Requirement | Test Level | Risk Link | Notes |
|---------|-------------|------------|-----------|-------|
| **C-02** | Concurrent revocations leave one live policy | API | R-01 | New |
| **C-10** | Each backup task failure raises an alert email | Manual | R-05 | New (#50) |
| **C-11** | Join Request over limit gets 429; response otherwise identical | API | R-06 | New (#55) |
| **C-12** | SMTP down: request succeeds; email arrives after recovery | API | R-07 | New (#56) |
| **C-13** | Post-deploy smoke on Test: migrator done, login, browse, play, upload converts | Manual (scriptable) | R-08, R-09 | New (#48, #49) |
| **C-20** | Upload validation, conversion Ready/Failed, write-once blobs, 24 h clean-up | API | R-09 | Exists |
| **C-21** | Search rules and 48-item paging | API | — | Exists |
| **C-22** | Playlist rules, Unavailable snapshot and revival | API | R-14 | Exists |
| **C-23** | Ten emails to the right people, Admin exceptions | API | R-07 | Exists |
| **C-24** | Queue, shuffle, repeat, Previous, link refetch, skip, persistence | Unit | R-15 | Exists (~98 tests) |
| **C-26** | Push protection on; secrets only on server | CI/config | R-12 | Exists |

**Total P1:** 11 scenarios

### P2 (Medium)

| Test ID | Requirement | Test Level | Risk Link | Notes |
|---------|-------------|------------|-----------|-------|
| **C-14** | Rollback by tag restores previous version | Manual | R-08 | New |
| **C-15** | Exploratory checklist: pages, drag-and-drop, Media Session, phone layout, loading screen | Manual | R-10 | New, per release |
| **C-16** | Suite duration and flake tracking | CI | R-11 | New |

**Total P2:** 3 scenarios

### P3 (Low)

| Test ID | Requirement | Test Level | Risk Link | Notes |
|---------|-------------|------------|-----------|-------|
| **C-25** | Conversion time measured on TrueNAS | Manual | R-16 | After launch |

**Total P3:** 1 scenario

---

## Execution Strategy

Run everything automated on every push unless it is expensive or long-running. The tool types are xUnit API tests (Aspire), xUnit client tests, and manual checks on Test. There are no k6 or chaos tiers, because no performance thresholds exist.

### Every push and PR (~current CI, 30-min limit)

- Both suites, all automated scenarios at any priority.
- If the run passes ~15 minutes, move slow conversion and revocation tests to a nightly job.

### Each deploy to Test

- C-13 smoke; C-03 once and after any storage change.

### Once before launch

- C-09, C-10, C-14, C-15.

### After launch

- C-25; repeat C-09 after a major PostgreSQL or TrueNAS upgrade.

---

## QA Effort Estimate (new work only)

| Priority | Count | Effort Range | Notes |
|----------|-------|--------------|-------|
| P0 | 6 new/extended | ~2-4 days | C-04 matrix is most of it |
| P1 | 5 new | ~1-3 days | Mostly rides along with #48, #50, #55, #56 |
| P2 | 3 new | ~0.5-1 day | |
| P3 | 1 new | ~1-2 hours | |
| **Total** | **15** | **~1-2 weeks of evenings** | **Eldon plus agents; existing ~375 tests need no new work** |

---

## Implementation Planning Handoff

| Work Item | Owner | Target | Notes |
|-----------|-------|--------|-------|
| Visibility matrix test (C-04) | agent | Next sprint | New ticket |
| Leave/Remove joint-Artist and preview cases (C-05, C-06) | agent | Next sprint | New ticket |
| Pending Changes fold cases (C-07, C-08) | agent | Next sprint | New ticket |
| Concurrent revocation test + rotation logging (C-02) | agent | Next sprint | New ticket |
| Real-Azure revocation check (C-03) and smoke (C-13) | Eldon | With #48 | Add to #48 acceptance criteria |
| Rate limit tests (C-11) | agent | With #55 | |
| Email retry tests (C-12) | agent | With #56 | |
| Restore rehearsal and alerts (C-09, C-10) | Eldon | #50, #51 | |

---

## Interworking & Regression

| Component | Impact | Regression Scope | Validation |
|-----------|--------|------------------|------------|
| **Azure Blob** | Upload, play links, revocation, CORS | C-01, C-02, C-20, `BlobCorsTests` | CI (Azurite) + C-03 on Test |
| **PostgreSQL** | Search, paging, migrations | C-21, `StartupTests` | CI |
| **SMTP** | Ten emails | C-12, C-23 | CI (MailPit) |
| **FFmpeg** | Conversion | C-20, C-13 | CI + Test |

Both suites must pass before a merge to `main`.

---

## Appendix A: Running Tests

```bash
# Everything (as CI does)
dotnet test KleinGeldMusic.slnx

# One class
dotnet test tests/KleinGeld.Api.Tests --filter "FullyQualifiedName~TakedownTests"
```

## Appendix B: Knowledge Base References

- `risk-governance.md`: scoring
- `test-levels-framework.md`: level choice
- `test-quality.md`: no hard waits, isolation, explicit assertions
- `nfr-criteria.md`, `adr-quality-readiness-checklist.md`: NFR planning

---

**Generated by:** BMad TEA Agent
**Workflow:** `bmad-testarch-test-design`
**Version:** 4.0 (BMad v6)
