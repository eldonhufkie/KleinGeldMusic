---
stepsCompleted: ['step-01-preflight', 'step-02-select-framework', 'step-03-scaffold-framework', 'step-04-docs-and-scripts', 'step-05-validate-and-summary']
lastStep: 'step-05-validate-and-summary'
lastSaved: '2026-11-17'
---

# Framework Setup Progress

## Step 1: Preflight

- Stack: **fullstack**. Backend is .NET 10 (Aspire AppHost, ASP.NET Core host, worker, migrator, xUnit suites). Frontend is a Blazor WebAssembly client served by the same host. There is no `package.json` yet; Node 22.19 and npm 11.15 are installed.
- No existing E2E framework (`playwright.config.*` / `cypress.config.*` absent).
- Backend framework already in place: xUnit + `Aspire.Hosting.Testing` (`tests/KleinGeld.Api.Tests`, `tests/KleinGeld.Web.Client.Tests`). Not re-scaffolded.
- App start: `dotnet run --project src/KleinGeld.AppHost` (web on https://localhost:7201). The Admin is seeded from the `admin-email`/`admin-password` parameters. The `/testing/*` hooks need `Testing__Enabled=true` on the web host. MailPit uses a dynamic port in run mode.
- Context docs: issue #3, `_bmad-output/test-artifacts/test-design-*.md` (R-10: UI not automated).
- Auth: ASP.NET Identity cookie from the static SSR form at `/Account/Login`; no bearer tokens.

## Step 2: Framework Selection

- **Playwright (TypeScript)** for the browser suite in `e2e/`. Eldon chose this; it matches how Playwright is used elsewhere: JS/TS spec files that drive the UI.
- Backend framework stays **xUnit** (already present).
- Reason for Playwright over Cypress: it runs WebAssembly apps across Chromium, Firefox and WebKit, has phone viewport emulation for the phone layout, supports media playback checks, and has built-in drag-and-drop.

## Step 3: Scaffold

- Execution mode: sequential.
- `e2e/` package: `@playwright/test` 1.65.1, `@seontechnologies/playwright-utils` 4.6.1 (install approved by Eldon), `@faker-js/faker`, `typescript`, `dotenv`. `.npmrc` `ignore-scripts=true` (the playwright-utils postinstall is bash-only).
- `playwright.config.ts`: timeouts action 15 s / navigation 30 s / test 60 s; trace `retain-on-failure-and-retries`, screenshot and video on failure; list + HTML + JUnit; `chromium` and `phone` (Pixel 7, `@phone`) projects; `webServer` = AppHost with empty database, MailPit on 8025, fixed Admin.
- Fixtures: `support/merged-fixtures.ts` (apiRequest, recurse, interceptNetworkCall, network-error-monitor, auth), `support/auth-fixture.ts` + `support/auth/kleingeld-auth-provider.ts` (all `AuthProvider` members, Identity cookie, `admin` + Members created through the Join Request flow), `support/global-setup.ts`.
- Factories: `member-factory.ts`, `release-factory.ts` (Faker). Helpers: identity forms, MailPit, blob upload.
- Samples: `tests/api/release-visibility.spec.ts` (P0, R-02), `tests/ui/player-keeps-playing.spec.ts` (P1, R-10).
- Pact: skipped; no consumer-provider boundary (the client calls its own host).
- AppHost: optional `MailPit:Port` pins MailPit's HTTP port in run mode.

## Step 4: Docs and Scripts

- `e2e/README.md`; npm scripts `test`, `test:headed`, `test:ui`, `test:p0`, `report`, `typecheck`.
- Write-time hook: `.claude/hooks/tea-enforce.cjs` (byte copy), `.claude/settings.json` (Pre/Post/Stop), `.tea/enforce-config.json` gated to `e2e/**/*.spec.{ts,js}` (no xUnit rules exist in the hook).

## Step 5: Validation

- `tsc` clean. Two consecutive runs: 2/2 passed (~40 s including app start when reused, ~57 s cold).
- Playwright Utils deviations (all with comments in code):
  - `support/helpers/mailpit.ts`: raw request to MailPit (not an app endpoint; apiRequest resolves against the app baseURL).
  - `support/helpers/blob-storage.ts`: raw PUT to blob storage (SAS link, binary body).
  - `support/factories/member-factory.ts`: raw requests inside the auth provider (mandate exception).
- Open: no CI job yet (`bmad-testarch-ci`).
