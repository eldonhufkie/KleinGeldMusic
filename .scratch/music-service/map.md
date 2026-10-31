# Map: KleinGeld Music first version

Label: wayfinder:map

## Destination

A buildable spec for a web-only first version of KleinGeld Music: Members join through approved Join Requests, upload Releases under Artists, the Admin reviews Releases before they're Published, and Members stream them and make Playlists. No payments. Hand off to `/to-spec` when no tickets remain.

## Notes

- Domain: an invite-only music platform for a small group (up to ~50 Members, a few thousand Tracks). Vocabulary lives in [GLOSSARY.md](../../GLOSSARY.md); use its terms (Member, Artist, Release, Release type, Track, Single mark, Playlist, Join Request, Admin, Rights Declaration, Takedown, Unavailable Track).
- Purpose: learning/portfolio **and** something the group actually uses. Prefer well-made architecture, but don't gold-plate.
- Fixed stack: .NET 10, ASP.NET Core API, .NET Aspire for local orchestration. Front end: Blazor WebAssembly with global interactivity (see the front-end decision).
- Hosting: the owner's TrueNAS SCALE server, reached via Tailscale Funnel; audio in Azure Blob Storage (see the hosting decision).
- No custom domain until after a working first version.
- Language: the UI and emails are English only for now.
- Source code is moving from Azure DevOps (`origin`) to a public GitHub repository, with GitHub Actions for CI/CD (see the deployment decision). Tracker is local markdown in this folder.
- [microsoft/dotnet-podcasts](https://github.com/microsoft/dotnet-podcasts) is reference material only: archived, and too complex to build on.
- Grilling tickets: call the Skill tool for "grilling" and "domain-modeling".
- Carry into the spec (setup chores, not decisions): provisioning the Azure storage account and its CORS rules for the `ts.net` address, Tailscale Funnel setup, a persistent volume for ASP.NET Core Data Protection keys, the dedicated Gmail account and its app password; moving the repository to GitHub with push protection; the Tailscale OAuth client, `tag:ci` ACL and the `deploy` user with its forced-command key; the `kleingeld` and `kleingeld-test` dataset folders and custom-app stubs; the backup snapshot tasks, `pg_dump` cron job, Cloud Sync tasks, backup storage account and TrueNAS alerts (see the backups decision).

### Decisions made while charting

- Release stages are Draft → Submitted → Published / Rejected. Editing title, cover art or description after publishing needs no review; adding, removing or replacing Tracks sends the Release back to Submitted.
- Artists can Take Down a Release (hide it, can be resubmitted) or Delete it (removed). Either way its Tracks become Unavailable Tracks on Playlists.
- Any Member can create an Artist; an Artist can be managed by several Members.
- Joining: an outsider submits a Join Request on a public landing page (name, email, message); the Admin approves; an approved person gets an email to set up their login.
- Rights: every Release carries a Rights Declaration **and** is reviewed by the Admin before publishing.
- A Track belongs to exactly one Release. A Track can carry a Single mark ('S' badge).
- Playlists are private to their Member.
- Front-end options considered: (A) Blazor WebAssembly with global interactivity, (B) Blazor Server with global interactivity, (C) Blazor static SSR + enhanced navigation with the player marked `data-permanent`, (D) a JS single-page app. Only A and C are prototyped; B and D stay noted as alternatives.

## Decisions so far

<!-- one line per resolved ticket: [title](issues/NN-slug.md): gist -->

- [What can we take from dotnet-podcasts?](issues/01-dotnet-podcasts-lessons.md): copy its MainLayout + PlayerService player (fits Blazor WebAssembly); avoid its many services, public audio URLs and localStorage-only lists.
- [Login and identity options](issues/04-identity-options.md): ASP.NET Core Identity; Admin approval creates a passwordless Member and emails a reset-token link; Register page removed.
- [Email sending options](issues/05-email-sending.md): Resend over SMTP/MailKit from our own domain, MailPit locally; ACS Email (retiring) and direct sending ruled out.
- [Hosting: Azure vs VPS vs home server](issues/02-hosting-options.md): Hetzner VPS + Aspire Docker Compose (~€12–30/mo) leads; Azure Container Apps easiest but ~$50–85/mo; home server cheapest but riskiest.
- [Choose hosting, storage and database](issues/07-hosting-storage-database.md): TrueNAS SCALE via Aspire Docker Compose, public through Tailscale Funnel; audio in Azure Blob (Azurite in dev) with SAS upload and ~1 h SAS playback links; PostgreSQL.
- [Audio pipeline: formats, conversion, streaming](issues/03-audio-pipeline.md): keep originals, convert to 192 kbps AAC .m4a with FFmpeg, stream via HTTP range requests through the API with a cookie check; no HLS.
- [Front end: Blazor WebAssembly vs static SSR with a persistent player](issues/06-front-end-prototype.md): Blazor WebAssembly with global interactivity; player in `MainLayout` with a `PlayerService`; accepts a ~2.5 MB first load.
- [Choose login and email approach](issues/08-login-and-email.md): ASP.NET Core Identity, email + password (passkeys and 2FA optional); every emailed link valid 15 min; Gmail SMTP with an app password until a domain allows Resend; five transactional emails.
- [Player features and first load for the first version](issues/09-player-and-first-load.md): volume, shuffle, repeat, Play next / Add to queue; play replaces the Queue with the list played from; Queue and settings in `localStorage`; Media Session; no prerendering, loading screen instead.
- [Release and Track details at upload](issues/10-release-and-track-details.md): Release type Album/EP/Single; required and optional fields; 500 MB audio, 1–50 Tracks, square ≥1000 px JPEG/PNG cover; Submit only when all Tracks converted; only Track changes need re-review; replacing keeps the Track.
- [Admin review of Submitted Releases](issues/11-admin-review.md): Review page + badge + Admin email; whole-Release approve/reject with a required reason; Withdraw; Track changes on Published Releases wait as Pending Changes while the old version stays live; Admin rejects or deletes (never Takedown); nine emails in total.
- [Deploying to a TrueNAS custom app from Azure DevOps](issues/15-deploy-to-truenas-research.md): 24.10 is archived and can't store registry credentials (25.04+ can); routes are a self-hosted agent, or a hosted agent joining the tailnet then SSH/`midclt`; Aspire's `.env` compose output needs an `include:` wrapper; migrations via bundle, migrator worker, or `MigrateAsync()`.
- [Choose how new versions are deployed](issues/16-choose-deployment.md): TrueNAS 25.10; custom app is an `include:` stub with hand-written `secrets.env`; migrator worker plus `pg_isready` healthcheck; public GitHub repo, Actions, public GHCR images; runner joins Tailscale and SSHes to a forced-command `deploy.sh`; merge to `main` deploys to a tailnet-only Test, a "Live" approval deploys the same tag to Production.
- [When a Member leaves or is removed](issues/12-member-leaves.md): Leave and Remove are permanent deletion of all personal data; an Artist goes with its last managing Member; managers add each other directly, all equal; Remove needs a reason (email 10); sessions cut off at once; no rejoin blocklist; the Admin can't Leave.
- [Browsing, navigation and search](issues/13-browsing-and-search.md): sidebar (bottom tab bar on phones); home shows New Releases and Playlists; only Published shown, others "not found"; Release type chips and Artist page sections; My Artists pages; Load more paging; search over names and titles with `pg_trgm` + `unaccent`; GUID v7 ids with slugs in URLs (ADR 0002).
- [Backups for the database and audio](issues/14-backups.md): at most 1 h loss, same-evening restore, 30-day retention everywhere; hourly/daily ZFS snapshots; hourly encrypted `pg_dump` to an immutable Azure account in another region; blob soft delete, write-once blob names, hourly Cloud Sync of audio home; restore rehearsed once before launch.

## Not yet specified

<!-- all earlier fog graduated into tickets on 2026-10-31; Playlist editing graduated into its own ticket the same day -->

## Out of scope

- Payments of any kind (tips, paid downloads, subscriptions).
- Native mobile apps (a later effort could use .NET MAUI over the same API).
- Offline downloads.
- Recommendations, likes, follows, comments, lyrics, statistics for artists.
- Shared or collaborative Playlists.
- A licensed commercial catalog.
- Public sign-up without Admin approval.
- Handing the Admin role to another Member (a manual database change if ever needed); decided in [When a Member leaves or is removed](issues/12-member-leaves.md).
- An Artist photo and bio (Artist cards use the newest Release's cover); an easy addition after launch. Decided in [Browsing, navigation and search](issues/13-browsing-and-search.md).
- Buying a custom domain (planned after a working first version; then Resend email and a nicer address than `ts.net`).
- A portfolio showcase site (e.g. on Vercel). Hosting the front end on Vercel would break the same-origin login cookie the audio stream relies on; decided in [Choose how new versions are deployed](issues/16-choose-deployment.md).
