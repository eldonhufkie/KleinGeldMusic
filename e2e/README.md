# Browser tests (Playwright)

These tests drive KleinGeld Music in a real browser. They cover what the xUnit suites can't: pages, the player across navigation, drag-and-drop, Media Session and the phone layout (test design risk R-10). Business rules stay in `tests/KleinGeld.Api.Tests`. Add a browser test only when a rule depends on the browser.

## Setup

Needs Node 22 (`.nvmrc`), the .NET SDK from `global.json`, Docker (for PostgreSQL, Azurite and MailPit) and FFmpeg on PATH (for the conversion worker).

```bash
cd e2e
npm ci
npx playwright install chromium
cp .env.example .env   # optional; the defaults work
```

`.npmrc` sets `ignore-scripts=true`. The playwright-utils `postinstall` runs a bash-only git command meant for its own repository, and nothing here needs install scripts.

## Running

```bash
npm test               # everything; starts the AppHost if nothing is listening on https://localhost:7201
npm run test:headed    # watch the browser
npm run test:ui        # Playwright UI mode: pick tests, time-travel, inspect
npm run test:p0        # only [P0] tests
npm run report         # open the last HTML report
npx playwright test --debug tests/ui/player-keeps-playing.spec.ts
```

Playwright starts the app with:

```bash
dotnet run --project ../src/KleinGeld.AppHost --launch-profile https -- \
  --Postgres:DataVolume=false --MailPit:Port=8025 \
  --Parameters:admin-email=admin@kleingeld.test --Parameters:admin-password=Admin-Pa55word!
```

To keep the app running between test runs, start it yourself with exactly those arguments; Playwright reuses it. Without `--Postgres:DataVolume=false` your dev data is used, and without the Admin parameters login fails.

## How it is built

| Path | What it is |
|------|------------|
| `playwright.config.ts` | Timeouts, artifacts (trace, screenshot, video on failure), HTML + JUnit reports, the `chromium` and `phone` projects, and the AppHost as `webServer` |
| `support/merged-fixtures.ts` | **The only place specs import `test` from.** Merges playwright-utils (`apiRequest`, `recurse`, `interceptNetworkCall`, network error monitor) with the auth fixture |
| `support/auth-fixture.ts`, `support/auth/kleingeld-auth-provider.ts` | playwright-utils `auth-session` with a provider for the ASP.NET Identity cookie. `page`, `context` and `authToken` act as a Member; `adminToken` is the Admin's cookie for setup |
| `support/global-setup.ts` | Clears `.auth/` (sessions only fit the database they were made in), then logs in the Admin and creates the default Member once |
| `support/factories/` | `createMember` (Join Request → approval → email 1 → set password) and `createPublishedRelease` (cover and audio to blob storage → conversion → submit → approve). Faker gives unique names |
| `support/helpers/` | Identity form fields, MailPit, blob uploads |
| `fixtures/` | `cover-1000.png` (square, 1000 px) and `tone-60s.mp3`, long enough to keep playing across navigation |
| `tests/ui/`, `tests/api/` | Specs |

### Who a test is

```ts
import { test, expect, ADMIN, asMember } from '../../support/merged-fixtures';

test('as the default Member', async ({ page, authToken }) => { /* ... */ });

test.describe('as the Admin', () => {
  test.use({ authOptions: { userIdentifier: ADMIN } });
  // ...
});

test.describe('as an outsider', () => {
  test.use({ authSessionEnabled: false });
  // ...
});
```

Any other `userIdentifier` (e.g. `'member-2'`) creates a new Member the first time it is asked for. API calls act as a Member by sending the cookie: `apiRequest({ ..., headers: asMember(authToken) })`.

## Practices

- **Selectors:** roles and accessible names first (`getByRole('button', { name: 'Play Intro' })`); the app already labels its controls. Add `data-testid` only where no accessible name exists.
- **Set up through the API, assert through the UI.** Factories create data over HTTP; the browser part tests only what a browser shows.
- **Never wait for time.** Wait for a request (`interceptNetworkCall`, declared before the action), a locator (`expect(...).toBeVisible()`), or a condition (`recurse`). `waitForTimeout` is blocked by the hook below.
- **Isolation:** each run starts from an empty database; every test makes its own Artists and Releases with unique names, so tests run in parallel without cleanup.
- **Tags:** put `[P0]`..`[P3]` in the title (test design priorities) and `@phone` for tests that run in the phone project only.

## Write-time enforcement

`.claude/hooks/tea-enforce.cjs`, registered in `.claude/settings.json`, checks every `e2e/**/*.spec.{ts,js}` as Claude Code writes it: disabled or focused tests (`.skip`, `.only`), hard waits, assertions that can't fail, files over 1,000 lines. Severity comes from TEA's test-review criteria registry, not from the hook. To turn a rule off, add its id to `disabledRules` in `.tea/enforce-config.json` and give the reason in the commit. C# tests aren't covered: the hook has no xUnit rules.

## CI

Not wired yet. A job needs Docker, FFmpeg, the .NET SDK, Node 22, `npm ci`, `npx playwright install --with-deps chromium`, then `npm test` with `CI=1` (no server reuse, 2 retries, 2 workers). Upload `e2e/playwright-report/` and `e2e/test-results/` as artifacts. TEA's `bmad-testarch-ci` workflow can scaffold it.

## References

TEA knowledge base: `playwright-utils-mandate.md`, `auth-session.md`, `api-request.md`, `recurse.md`, `intercept-network-call.md`, `network-error-monitor.md`, `fixtures-composition.md`, `data-factories.md`, `test-quality.md`.
