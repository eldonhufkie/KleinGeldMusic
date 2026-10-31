# Player features and first load for the first version

Type: grilling
Status: resolved
Assignee: Eldon Hufkie
Blocked by: 06

## Question

Now that the front end is Blazor WebAssembly with a `PlayerService` in `MainLayout` (see [Front end: Blazor WebAssembly vs static SSR with a persistent player](06-front-end-prototype.md)), what does the player do in the first version, and what does a Member see on first load?

- **Player features** beyond play/pause/seek/next/previous: an explicit queue ("play next", "add to queue"), shuffle, repeat, volume.
- **What playing a Track queues**: the rest of its Release, or of its Playlist?
- **Unavailable Tracks**: what happens to the queue when it reaches one, or when the Track that is playing becomes unavailable.
- **Persistence**: whether the queue and position survive a page reload.
- **Lock screen and hardware media keys**: whether the browser's Media Session API should show the Track there.
- **First load**: keep prerendering (the page shows at once, but its buttons do nothing for a second or two), or turn it off and show a loading screen?

## Answer

Decided by Eldon Hufkie on 2026-10-31 in a grilling session.

- **Controls**: play/pause, seek, next, previous, volume with mute, shuffle, repeat (off / all / one). "Play next" and "Add to queue" on any Track. An up-next panel can remove Tracks but not reorder them (drag-to-reorder later).
- **Filling the Queue**: pressing play on a Track replaces the whole Queue (no confirmation) with the list it was played from, starting at that Track: a Release in Track order, a Playlist in its order, or an Artist page or search results as listed.
- **Shuffle**: the pressed Track plays first, the rest follow in random order. Turning shuffle on mid-Queue shuffles only the Tracks still to come; turning it off restores their original order after the current Track. Tracks added with "Play next" or "Add to queue" keep their place.
- **Previous, end of Queue, repeat**: "Previous" restarts the current Track if it is more than 3 s in, otherwise goes back one. With repeat off the player stops at the end of the Queue and stays on the last Track. Repeat-one loops only when a Track ends on its own; "Next" still moves on.
- **Unavailable Tracks**: left out when the Queue is built. If the playing Track becomes unavailable, its next range request fails and the player skips to the next playable Track with a short notice. No attempt to finish it.
- **Persistence**: Queue, current Track, position, volume, mute, shuffle and repeat kept in `localStorage`; restored paused after a reload, dropping Tracks that have since become unavailable. If storage fails, start empty with defaults (full volume, shuffle off, repeat off).
- **Media Session API**: yes: title, Artist and cover art on lock screens; hardware media keys and headphone buttons work.
- **First load**: no prerendering. A lightweight loading screen (logo plus a progress bar from Blazor's load-progress CSS variables) shows until WebAssembly is ready. The public landing page with the Join Request form is part of the same app.
- **Multiple tabs or devices**: each plays on its own, with no coordination.
- **Glossary**: added **Queue**.
- No ADR: all of this is cheap to change later.


**Amended 2026-10-31** (streaming reconciliation, see the [spec](../spec.md)): play links are ~10-minute SAS links signed against a stored access policy that the API rotates whenever access is taken away, so loss of access applies within about 30 seconds; the player fetches a fresh link when playback fails.
