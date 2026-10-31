# 36: Add from anywhere, no duplicates, limits

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** A Member adds Tracks to Playlists from every Track menu, adds whole Releases, and creates a Playlist from the menu.

**Blocked by:** 35 (Create a Playlist and add Tracks)

**Status:** ready-for-agent

- [ ] Add to Playlist in every Track menu: Release page, Playlist page, search results, Queue panel
- [ ] "New Playlist…" at the top of the menu creates a Playlist and adds the Track
- [ ] A Track appears at most once; adding it again says "Already on <Playlist>"
- [ ] Add whole Release (button at the top of the Release page) adds missing Tracks in order and reports how many were skipped
- [ ] At most 100 Playlists per Member and 1,000 Tracks per Playlist, with clear messages
- [ ] API tests (Aspire testing seam) for duplicates, skipped count and both limits
