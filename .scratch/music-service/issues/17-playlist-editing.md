# Playlist editing

Type: grilling
Status: resolved
Assignee: Eldon Hufkie
Blocked by: —

## Question

How does a Member manage their Playlists in the first version?

- Creating, renaming and deleting a Playlist; whether a Playlist has anything beyond a name (description, cover).
- Adding Tracks (from a Release page, a Track menu, the Queue, a whole Release at once); whether the same Track may appear twice.
- Reordering and removing Tracks, on desktop and on phones.
- Unavailable Tracks (see [When a Member leaves or is removed](12-member-leaves.md) and [Browsing, navigation and search](13-browsing-and-search.md)): how they show, and whether they can be cleared in bulk.
- Any limits on Playlist size or the number of Playlists per Member.

## Answer

Decided by Eldon Hufkie on 2026-10-31 in a grilling session.

- **What a Playlist has**: a name only (required, 1–100 characters, duplicates allowed). Its cover is the cover of its first playable Track's Release. No description, no uploaded cover.
- **Create, rename, delete**: "New Playlist" on the Playlists page, and "New Playlist…" at the top of every "Add to Playlist" menu (creates it and adds the Track in one step). Rename from the Playlist page. Delete asks for confirmation and is permanent.
- **Adding**: "Add to Playlist" in every Track row menu (Release page, Playlist page, search results, Queue panel) and a button at the top of the Release page that adds the whole Release in Track order. New Tracks go to the end. No "Save Queue as Playlist", no adding a whole Artist.
- **No duplicates**: a Track appears at most once on a Playlist. Adding one already there says "Already on <Playlist>"; adding a whole Release adds only the missing Tracks and says how many were skipped.
- **Reordering**: a drag handle on every row, for mouse and touch alike (e.g. SortableJS via JS interop); no edit mode, each move saves at once; no extra Move up/down items on phones.
- **Removing**: "Remove from Playlist" in the row menu acts at once, with an Undo notice for a few seconds; no confirmation.
- **Unavailable Tracks**: keep their title and Artist name as they were, greyed out with an "Unavailable" label and no links, even after the Release or Artist is deleted (so a Playlist entry stores that much itself). If the Release comes back (resubmitted after a Takedown, or a Rejected Release approved again), the Track is playable again in the same place.
- **Bulk clear**: a Playlist with Unavailable Tracks shows "Remove unavailable Tracks (n)", with confirmation; single ones can still be removed one by one.
- **Limits**: at most 100 Playlists per Member and 1,000 Tracks per Playlist, with a clear message at the limit.
- **"Most recently changed"** (the Playlists page order) counts every edit: rename, add, remove, reorder, bulk clear.
- **Concurrency**: editing a Playlist doesn't touch a Queue already built from it; the Queue picks up changes the next time play is pressed. Each edit is a small server-side action (add, remove, move), last write wins, no conflict warnings across tabs or devices.
- **Glossary**: **Playlist** now says a Track appears on it at most once; **Unavailable Track** now says it becomes playable again if its Release is Published again.
- No ADR: all of this is cheap to change before launch.
