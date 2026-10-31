# 01: Walking skeleton

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** The whole system starts with one command and a visitor sees the public landing page. Aspire AppHost and ServiceDefaults orchestrate the ASP.NET Core host (API plus Blazor Web App host), the Blazor WebAssembly client (global interactivity, no prerendering, loading screen with load progress), PostgreSQL with a migrator worker that the API waits for, Azurite and MailPit. Both test seams exist with one passing test each, and GitHub Actions builds and tests every push and PR.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Starting the AppHost brings up the API/host, client, PostgreSQL, migrator, Azurite and MailPit; the migrator runs migrations and exits before the API starts
- [ ] The landing page renders after a loading screen driven by Blazor's load-progress CSS variables; no prerendering
- [ ] The system's clock is a `TimeProvider` that tests can replace
- [ ] One API test (Aspire testing seam) starts the distributed app and gets a successful response from the API
- [ ] One PlayerService test runs against a fake JS audio module
- [ ] A GitHub Actions workflow builds and runs all tests on every push and PR to `develop` (runner has Docker and FFmpeg); it never deploys
