# Browsing, navigation and search

Type: grilling
Status: resolved
Assignee: Eldon Hufkie
Blocked by: 10

## Question

How does a Member find music in the first version?

- The pages and main navigation: home, Artists, Releases, an Artist page, a Release page, Playlists, the Member's own Artists, Admin pages.
- What the home page shows (newest Releases? a random pick?).
- Sort orders and paging for lists of a few thousand Tracks.
- What "basic search" covers: Artists, Releases, Tracks; title only or also description; how matching works (PostgreSQL `ILIKE`, trigram, full-text).
- Where Single badges and Release types (from [Release and Track details at upload](10-release-and-track-details.md)) appear.

## Answer

Decided by Eldon Hufkie on 2026-10-31 in a grilling session.

- **Navigation**: a sidebar beside the player in `MainLayout` with Home, Artists, Releases, Search, Playlists (the Member's own listed underneath), My Artists (only for Members who manage an Artist) and Admin (Admin only, with the review badge). A profile menu at the top right holds account settings and Leave. No Tracks page. Below ~700 px the sidebar becomes a bottom tab bar (Home, Search, Releases, Playlists, More) with the mini player just above it.
- **Home**: a New Releases row (the 12 most recently Published, ordered by Published date, not release date) and a Your Playlists row. No random pick or "recently played" (no listening history on the server).
- **What browsing shows**: only Published Releases, in their live version (Pending Changes are invisible), and only Artists with at least one Published Release. Any other Release's URL shows "not found" to everyone except its managing Members and the Admin, so nothing leaks about unpublished work.
- **Artists list**: an A–Z grid; each card uses the cover of the Artist's newest Release. No Artist photo or bio (out of scope).
- **Releases list**: a cover grid with filter chips All / Albums / EPs / Singles; sort Newest (Published date, the default) or A–Z.
- **Artist page**: the name, then Albums, EPs and Singles sections, each newest release date first; empty sections hidden. Play plays the newest Release. No separate row of Tracks with the Single mark; the badge is enough.
- **Release page**: cover, title, Artist, Release type, release date, genre, total duration, description, then the numbered Track list. Each row: title, featured Artists, badges, duration, and a menu (Play next / Add to queue / Add to Playlist). Clicking a row plays the Release from that Track.
- **Badges**: 'S' (Single mark) and 'E' (explicit) appear on every Track row in the app (Release page, Playlist, Queue, search results) and never on Release cards.
- **My Artists**: a page per managed Artist listing all its Releases with a stage badge (Draft / Submitted / Published / Rejected with reason / Taken down) and "Pending Changes" where there are any; holds the New Release button and the managers list. The public Artist page stays a listener's view, even for its managers.
- **Playlists page**: the Member's Playlists, most recently changed first. **Playlist page**: name, Track count, total duration, Play and Shuffle, Track rows as on the Release page plus Artist and Release links; Unavailable Tracks stay in place, greyed out and skipped by the player. Editing a Playlist is left to fog.
- **Paging**: "Load more" with keyset paging, 48 items at a time.
- **Search scope**: one box, three grouped sections: Artists (name), Releases (title), Tracks (title and featured Artists). Descriptions and genre aren't searched.
- **Matching**: PostgreSQL `pg_trgm` plus `unaccent` with GIN trigram indexes; case- and accent-insensitive. At 2 characters, a prefix match; from 3, trigram similarity ranked by score. Results update as you type (debounced ~300 ms); each section shows its top 5 with "Show all" opening that section in full with Load more. The query lives in the URL (`/search?q=…`) so Back works. Plain `ILIKE` (no typo tolerance) and full-text search (built for prose) were ruled out.
- **URLs and ids**: every entity (Artist, Release, Track, Playlist) has a GUID v7 id; URLs carry id plus slug, e.g. `/artists/{id}/de-kleine-kapel`; the slug is decoration and is corrected after a rename. Recorded in [ADR 0002](../../../docs/adr/0002-guid-v7-ids-in-urls.md).
- No glossary changes: everything here is UI, not domain language.
