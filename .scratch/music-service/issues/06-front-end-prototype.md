# Front end: Blazor WebAssembly vs static SSR with a persistent player

Type: prototype
Status: resolved
Assignee: Eldon Hufkie
Blocked by: 01

## Question

Which front-end approach should KleinGeld Music use, given that the audio player must keep playing, uninterrupted, while a Member navigates between pages?

Build the same tiny app twice (two or three pages, e.g. an Artist page and a Release page, plus a player bar that plays a sample Track):
- **(A)** Blazor WebAssembly with global interactivity; the player lives in `MainLayout`.
- **(C)** Blazor static SSR with enhanced navigation; the player is a small JS-driven element marked `data-permanent`.

Click between pages while music plays and judge: does playback ever stutter or restart? How does the first load feel? How awkward is the player code in each? How would the player show which Track is playing on the page being viewed?

Alternatives noted but not prototyped: (B) Blazor Server with global interactivity, (D) a JS single-page app.

Use what [What can we take from dotnet-podcasts?](01-dotnet-podcasts-lessons.md) found about its persistent player.

## Assets

- Prototype: `prototypes/front-end-player/` on branch `prototype/front-end-player` (commit `49c6a1f`, local only, never merged). Run instructions and things to try are in its `README.md`.

## Answer

**Option A: Blazor WebAssembly with global interactivity.** Decided by Eldon Hufkie on 2026-10-31 after trying both prototypes.

- **Player design**: one `<audio>` element and the player bar in `MainLayout`, outside `@Body`. A single `PlayerService` (scoped, so one per browser tab) holds the queue and playback state and raises a change event. A small JS module drives the audio element. This is the dotnet-podcasts pattern.
- **Why A**: the prototype showed both A and C keep music playing, uninterrupted, across navigation, so A won on the other criteria:
  - The front end stays C#.
  - Interactive features (Playlists, uploads, Admin review) are ordinary components, with no hand-written JS for each feature.
  - The WebAssembly client calls the ASP.NET Core API that is planned anyway.
  - After the first load, the home server mostly serves API calls and audio.
- **Accepted cost**: about 2.2–2.5 MB (brotli) to download on the first visit and after each deploy, then cached. On localhost the player became usable after about 1.2 s, and it will be slower through Tailscale Funnel. Until WebAssembly has loaded, the prerendered page shows but its buttons don't work.
- **Measured in the prototype** (headless Chromium): playback never restarted and the `<audio>` element was never replaced across seven navigations, browser Back, and adding a Track to a Playlist. The now-playing row highlighted correctly.
- **Not chosen**:
  - C (static SSR with a `data-permanent` player): the player and every interactive feature would be JS, and any form without `data-enhance` stops the music.
  - B (Blazor Server): needs a live connection per Member, which a music player can't rely on.
  - D (a JS single-page app): leaves the .NET stack.
