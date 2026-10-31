# Release and Track details at upload

Type: grilling
Status: resolved
Assignee: Eldon Hufkie
Blocked by: —

## Question

What does an Artist fill in and upload when building a Draft Release, and what limits apply?

- **Release details**: title, cover art, description, release date; whether a Release also carries a type (Album / EP / Single) for browsing, separate from a Track's Single mark.
- **Track details**: which fields are required (title, Track number, ...) versus optional (credits, featured Artists, explicit flag, ...). Embedded tags only pre-fill the form (see [Audio pipeline: formats, conversion, streaming](03-audio-pipeline.md)).
- **Limits**: maximum audio file size, maximum Tracks per Release, cover-art formats, minimum and maximum dimensions, file size.
- **Before submitting**: what must be complete for a Draft to be Submitted (beyond the Rights Declaration), and what happens while Tracks are still converting.

## Answer

Decided by Eldon Hufkie on 2026-10-31 in a grilling session.

- **Release type**: every Release has one, chosen by the Artist: Album, EP or Single. No automatic rule from the Track count, no Compilation.
- **Release fields**: title (required), cover art (required to submit), release date (required, may be in the past; display and sorting only, nothing is scheduled), description (optional, plain text, ~2,000 chars), genre (optional, one free-text field). No label, UPC/catalogue number or copyright line.
- **Track fields**: title (required), Track number (its order in the Release, drag to reorder in a Draft), Single mark, featured Artists as free text, explicit flag (all optional). Duration from ffprobe. No disc numbers, per-Track credits or ISRC.
- **Explicit**: an 'E' badge only; no filtering.
- **Limits**: audio up to 500 MB per file, no length limit; 1 to 50 Tracks per Release. Cover art JPEG or PNG, square, at least 1000×1000 px, at most 20 MB; non-square is rejected (no crop tool). The original is kept plus resized copies (~300 px and ~1000 px). WebP later.
- **Pre-filling**: the first audio file fills empty Release title, release date, genre and cover art from its tags; each Track title comes from its tag, else the file name. Pre-filling never overwrites typed values. Embedded cover art goes through the cover-art checks and is skipped with a note if it fails.
- **Submitting**: Submit stays disabled until every Track has converted. Each Track shows Uploading / Converting / Ready / Failed; a Failed Track is removed or replaced by the Artist.
- **Rights Declaration**: a required checkbox at each Submit with fixed wording; stored per submission with the Member, time and wording version.
- **Who**: every Member who manages an Artist can create, edit, submit and Take Down its Releases equally. Simultaneous edits: last write wins.
- **Edits after publishing**: only adding, removing or replacing Tracks sends a Release back to review. All other Release and Track fields stay editable without review.
- **Replace and remove**: replacing a Track's audio keeps the same Track, so Playlist entries keep pointing at it. Members hear the old audio until the review passes. Removing a Track from its Release makes it an Unavailable Track on Playlists; it is never deleted outright while on a Playlist.
- **Clean-up**: a daily job deletes unattached uploaded blobs older than 24 h. Removing a Track from a Draft deletes its blobs at once. Drafts never expire.
- **Glossary**: added **Release type**; the Track mark is renamed **Single mark** (Single is now a Release type); **Unavailable Track** now also covers Tracks removed from their Release.
- No ADR: all of this is cheap to change later.
- Left to [Admin review of Submitted Releases](11-admin-review.md): whether the old version of a Published Release stays visible while its changes wait for review.
