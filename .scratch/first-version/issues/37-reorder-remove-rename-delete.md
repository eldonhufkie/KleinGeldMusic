# 37: Reorder, remove with Undo, rename, delete; automatic cover

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** A Member drags Playlist rows to reorder them, removes Tracks with a few seconds' Undo, renames and deletes Playlists, and sees an automatic cover.

**Blocked by:** 35 (Create a Playlist and add Tracks)

**Status:** ready-for-agent

- [ ] Drag handle on every row (mouse and touch, e.g. SortableJS via JS interop); each move saves at once
- [ ] Remove from Playlist acts at once with an Undo notice that puts it back in the same place
- [ ] Rename from the Playlist page; delete with confirmation is permanent
- [ ] The cover is the cover of the first playable Track's Release
- [ ] Every edit bumps "most recently changed"; a Queue already built from the Playlist isn't changed
- [ ] API tests (Aspire testing seam) for move, remove, undo, rename, delete and the cover rule
