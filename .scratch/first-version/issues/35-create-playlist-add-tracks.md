# 35: Create a Playlist and add Tracks

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** A Member creates a Playlist, adds Tracks to it from the Track row menu, and plays it from its page.

**Blocked by:** 19 (Approve: Published)

**Status:** ready-for-agent

- [ ] A Playlist has a name (1–100 chars, duplicates allowed) and is private to its Member
- [ ] Add to Playlist in Track row menus appends the Track; each entry stores the Track's title and Artist name
- [ ] Playlist page: name, Track count, total duration, Play and Shuffle, Track rows with Artist and Release links
- [ ] Playlists page lists them by most recently changed; the sidebar lists them under Playlists; home shows Your Playlists
- [ ] API tests (Aspire testing seam): another Member and the Admin get "not found" for someone else's Playlist
