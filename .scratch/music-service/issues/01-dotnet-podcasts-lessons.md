# What can we take from dotnet-podcasts?

Type: research
Status: resolved
Blocked by: —

## Question

[microsoft/dotnet-podcasts](https://github.com/microsoft/dotnet-podcasts) is a .NET podcast app (ASP.NET Core API, Blazor, MAUI, Azure). It's archived and too complex to build on, but it solves problems we share. Which of its patterns are worth copying for KleinGeld Music, and which should we avoid?

Specifically:
- How the web app keeps audio playing while the user navigates between pages (component placement, JS interop, render mode).
- How the solution is split into projects (API, shared models, front end) and what we'd simplify.
- How audio is served to the player (direct URLs, proxying, range requests).
- What Azure services it relies on, and which have a cheaper or simpler alternative at our scale.
- Anything that is outdated relative to .NET 10 / Aspire.

## Answer

- **Copy the player design**: one `<audio>` element plus the player bar in `MainLayout`, outside `@Body`, so navigation never touches them; a single `PlayerService` holds state and raises change events; a tiny JS module drives the audio element. It relies on the whole app running as WebAssembly, so it carries over almost unchanged to option A (Blazor WebAssembly, global interactivity). Option C (static SSR + `data-permanent`) would need the player rewritten in JS, and the sample offers no evidence for it.
- **Avoid**: its ~15 projects and 7 deployed services (workers, Orleans/SignalR hub, two SQL databases, a queue) with hand-wired URLs; audio played from public third-party URLs and cover art in a public container (our Tracks need an authorised path: API endpoint with range support or short-lived signed URLs, with cookie auth); lists kept only in `localStorage` (Playlists must live on the server).
- **Simplify and update**: one API project, one front end, an Aspire AppHost + ServiceDefaults, one database, private file storage. The sample is .NET 7 (archived 2024-05-06) and uses `_Host.cshtml`, `blazor.webassembly.js` and pre-release OpenTelemetry packages, all replaced by .NET 8+ Blazor Web Apps and Aspire.

Findings, with citations (source read at commit `5ee8be2`): `docs/research/dotnet-podcasts-lessons.md` on branch `research/dotnet-podcasts-lessons` (pushed to origin; never merged).
