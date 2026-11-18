---
title: 'TEA Test Design → BMAD Handoff Document'
version: '1.0'
workflowType: 'testarch-test-design-handoff'
inputDocuments: ['_bmad-output/test-artifacts/test-design-architecture.md', '_bmad-output/test-artifacts/test-design-qa.md']
sourceWorkflow: 'testarch-test-design'
generatedBy: 'TEA Master Test Architect'
generatedAt: '2026-11-16'
projectName: 'KleinGeldMusic'
---

# TEA → BMAD Integration Handoff

## Purpose

Carries the test design's risks and critical scenarios into ticket planning. This project plans in GitHub Issues (see `docs/agents/issue-tracker.md`), so "epic" means issue #3 and "story" means its sub-issues.

## TEA Artifacts Inventory

| Artifact | Path | Integration Point |
|----------|------|-------------------|
| Architecture test design | `_bmad-output/test-artifacts/test-design-architecture.md` | Risks, blockers, mitigation plans |
| QA test design | `_bmad-output/test-artifacts/test-design-qa.md` | Scenarios C-01..C-26, gates |
| Progress checkpoint | `_bmad-output/test-artifacts/test-design-progress-system.md` | Run history |

## Epic-Level Integration Guidance

### Risk References

- R-01 (SEC, 6): revocation on real Azure
- R-02 (SEC, 6): one visibility rule
- R-03 (DATA, 6): Leave/Remove deletion
- R-04 (DATA, 6): Pending Changes integrity
- R-05 (OPS, 6): backups and restore

### Quality Gates

- P0 100%, P1 >= 95%.
- R-01 to R-05 mitigated before Production launch.
- Both suites green before any merge to `main`.

## Story-Level Integration Guidance

### P0/P1 Scenarios → Acceptance Criteria

- **#48 Deploy workflow:** add C-13 (post-deploy smoke) and C-03 (link refused within ~30 s after Takedown on Test).
- **#49 Test seed:** seed supports C-13 (an Admin, Members, a Published Release with a playable Track).
- **#50 Backups:** add C-10 (each task failure sends an alert).
- **#51 Restore rehearsal:** C-09 with timings recorded.
- **#55 Rate limit:** C-11 (429 over limit; response unchanged under it).
- **#56 Background email:** C-12 (email delivered after SMTP recovers).
- **New tickets:** C-04 visibility matrix; C-05/C-06 Leave/Remove cases; C-07/C-08 Pending Changes fold; C-02 concurrent revocation + rotation logging.

### Data-TestId Requirements

N/A: no browser automation in v1.

## Risk-to-Story Mapping

| Risk ID | Category | P×I | Recommended Story | Test Level |
|---------|----------|-----|-------------------|------------|
| R-01 | SEC | 2×3=6 | New: concurrent revocation; #48 | API, Manual |
| R-02 | SEC | 2×3=6 | New: visibility matrix | API |
| R-03 | DATA | 2×3=6 | New: Leave/Remove cases | API |
| R-04 | DATA | 2×3=6 | New: Pending Changes fold | API |
| R-05 | OPS | 2×3=6 | #50, #51 | Manual |
| R-06 | SEC | 2×2=4 | #55 | API |
| R-07 | OPS | 2×2=4 | #56 | API |
| R-08 | OPS | 2×2=4 | #48, #49 | Manual |
| R-09 | TECH | 2×2=4 | #48 | Manual |

## Recommended Workflow Sequence

1. TEA Test Design (this run)
2. Create the new tickets above (triage as `ready-for-agent`)
3. TEA ATDD per ticket where useful
4. Implement via `/implement`
5. TEA Trace to confirm P0/P1 coverage

## Phase Transition Quality Gates

| From | To | Gate |
|------|----|------|
| Test Design | Tickets | Every high risk has a ticket |
| Tickets | Implementation | Ticket lists its C-xx scenarios as acceptance criteria |
| Implementation | Release | P0 100%, P1 >= 95%, R-01..R-05 mitigated |
