# Every entity has a GUID v7 id, shown in URLs with a slug

Artists, Releases, Tracks and Playlists all get a GUID id generated as version 7 (`Guid.CreateVersion7()`), and that id appears in page addresses followed by a readable slug, e.g. `/artists/0192f3a1-…/de-kleine-kapel`. The id finds the record; the slug is decoration, corrected if the title changes, so links shared within the group survive renames. GUIDs mean ids can't be guessed or counted, which matters because unpublished Releases must stay invisible. Version 7 is time-ordered, so it stays friendly to PostgreSQL's B-tree indexes.

## Considered Options

- **Integer ids** (`/artists/42/...`): shorter, nicer URLs, but enumerable: anyone can count Artists and Releases or probe for unpublished ones. Rejected.
- **Random GUIDs (v4)**: just as unguessable, but random insert order fragments indexes. Rejected in favour of v7.
- **Slug only** (`/artists/de-kleine-kapel`): prettiest, but breaks links on rename and needs uniqueness handling. Rejected.

## Consequences

- URLs are long; that's accepted since they're shared by link, not typed.
- Changing id type later would mean migrating every key and breaking every shared link, so this is fixed from the first migration.
