---
stepsCompleted: ['step-01-preflight-and-context', 'step-02-identify-targets', 'step-03c-aggregate', 'step-04-validate-and-summarize']
lastStep: 'step-04-validate-and-summarize'
lastSaved: '2026-11-18'
inputDocuments: ['_bmad-output/test-artifacts/test-design-qa.md', '_bmad-output/test-artifacts/test-design-architecture.md', 'e2e/playwright.config.ts', 'e2e/support/merged-fixtures.ts', 'knowledge/playwright-utils-mandate.md', 'knowledge/auth-session.md', 'knowledge/test-levels-framework.md', 'knowledge/test-quality.md', 'knowledge/risk-governance.md']
---

# Automation Summary: Playwright E2E (R-10)

## Step 1: Preflight and Context

- Stack: fullstack. Framework present: `e2e/playwright.config.ts`, `e2e/package.json` with `@playwright/test` and `@seontechnologies/playwright-utils`.
- Mode: **BMad-Integrated**. Source: system test design (R-10, C-15), issue #3 stories 13, 86, 96, 98, 105, 106 and 71.
- Flags: `tea_use_playwright_utils: true` (installed, so the mandate applies), `tea_browser_automation: auto` (playwright-cli not installed; pages read from source and from failure snapshots), Pact not relevant.

## Step 2: Targets and Coverage Plan

Scope is **selective**: only behaviour a browser alone shows. Rules already covered by xUnit (Playlist ordering, Undo, Queue logic) are exercised here only through their UI.

| ID | Scenario | Pri | Story / Risk | Spec | Result |
|----|----------|-----|--------------|------|--------|
| E-01 | Outsider opening a Member page is sent to Log in | P0 | 13 / R-10 | `ui/login-and-logout.spec.ts` | Pass |
| E-02 | Member logs in through the form and lands in the app | P1 | 13 | `ui/login-and-logout.spec.ts` | Pass |
| E-03 | Log out ends the session and clears the saved Queue | P1 | 98 | `ui/login-and-logout.spec.ts` | Pass |
| E-04 | Player keeps playing across pages | P1 | 86 | `ui/player-keeps-playing.spec.ts` | Pass |
| E-05 | Lock screen gets the Track title and Artist (Media Session) | P2 | 96 | `ui/player-keeps-playing.spec.ts` | Pass |
| E-06 | Dragging a Track by its handle reorders the Playlist and survives reload | P1 | 105 | `ui/playlist-reorder.spec.ts` | Pass (after defect fix) |
| E-07 | Arrow keys on the handle reorder too | P2 | 105 | `ui/playlist-reorder.spec.ts` | Pass |
| E-08 | Take off Playlist shows Undo; Undo puts the Track back in place | P1 | 106 | `ui/playlist-reorder.spec.ts` | Pass |
| E-09 | Phone: tab bar shown, sidebar hidden | P2 | 71 | `ui/phone-layout.spec.ts` (`@phone`) | Pass |
| E-10 | Phone: mini player sits above the tab bar while playing | P2 | 71 | `ui/phone-layout.spec.ts` (`@phone`) | Pass |

Plus the existing `api/release-visibility.spec.ts` (R-02 sample): Pass. No API or backend workers ran: the plan assigns them nothing, so playlist position rules, Undo position and Queue rules stay in the xUnit suites.

Totals: 10 E2E scenarios (P0 1, P1 5, P2 4). Run 2026-11-18: **11 passed** (incl. the API sample), after the drag fix below.

## Step 3: Generation and Aggregation

Execution mode: sequential (subagents are not started unless the user asks for them).

The specs had been written by the interrupted run. This run fixed them against the live app:

| File | Change | Why |
|------|--------|-----|
| `e2e/tests/ui/login-and-logout.spec.ts` | `Log in` button matched with `exact: true` | "Log in with a passkey" also matched (strict mode) |
| same | Email and profile menu scoped to `getByRole('group')` | A hidden copy of the profile menu sits in the phone tab bar |
| `e2e/tests/ui/playlist-reorder.spec.ts` | `beforeEach` waits for `**/track-reorder*.js` (fingerprinted) | Rows render before Blazor attaches the drag listeners; acting earlier is a race |
| same | Order is checked by entry id (`data-track-id`), and Undo by stored Track ids | All three Tracks share the `tone-60s` fixture and title, so title order proved nothing |

No new fixtures were needed. The existing auth-session provider, factories (`release-factory`, `playlist-factory`) and merged fixtures cover every spec.

## Step 4: Validation

- Framework ready; `tsc --noEmit` clean; full suite run on `chromium` and `phone` projects.
- Selectors are role/label based. The remaining CSS locators are structural: `ol.release-tracks li[data-track-id]` (row order), `summary` inside the profile-menu group, `audio` (playback position).
- No hard waits; polling uses `recurse`; app calls observed with `interceptNetworkCall` declared before the action.
- No playwright-cli sessions were opened. The temporary pointer-event probe spec was removed.

### Defect found and fixed: dragging a Track never saved (story 105)

`src/KleinGeld.Web/wwwroot/track-reorder.js` moves the dragged row with `insertBefore` during `pointermove`. Moving the row takes the captured handle out of the DOM and back, which **releases pointer capture**. Later `pointermove`/`pointerup` events go to other elements, the handle's `finish` listener never runs, the rows snap back and `Reorder` is never called. A probe confirmed it: after the first row move, the event target moves off `.track-handle`, and no PUT is sent. The same script drives the Release track list (`ReleaseTracks.razor`), so drag reorder there is affected too. Arrow keys work.

**Fixed:** `track-reorder.js` now listens for `pointermove`/`pointerup`/`pointercancel` on `document` for the length of the drag, filtered to the dragging pointer, instead of capturing the pointer on the handle. E-06 passes.

### Playwright Utils deviations

- `e2e/support/factories/release-factory.ts:69,84`: raw `request` for `putBlob` to an Azurite SAS URL. That is a storage upload outside the app's API, so `apiRequest` (base URL, app auth) does not apply.

No recommended utilities are missing: `auth-session` is wired (`support/auth/kleingeld-auth-provider.ts`). `network-recorder` and `burn-in` are not wired, and nothing in the plan needs them yet.

### Assumptions and risks

- Local runs reuse a running AppHost; it must use the Admin and MailPit settings in `e2e/README.md`. That depends on the uncommitted `MailPit:Port` change in `src/KleinGeld.AppHost/AppHost.cs`.
- Touch drag (story 105, "mouse and touch") is not covered separately. The same capture defect applies to it.

### Next

1. Commit the drag fix and the spec fixes.
2. `bmad-testarch-ci` to put this suite in the pipeline, then `bmad-testarch-test-review` or `bmad-testarch-trace` for the gate.
