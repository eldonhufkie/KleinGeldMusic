# 16: Pre-fill from tags

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** Uploading audio pre-fills empty details from its embedded tags, never overwriting typed values.

**Blocked by:** 11 (Cover art upload), 12 (Upload a Track)

**Status:** ready-for-agent

- [ ] The first audio file fills empty Release title, release date, genre and cover art (TagLib#)
- [ ] Each Track title comes from its tag, else the file name
- [ ] Typed values are never overwritten
- [ ] Embedded cover art goes through the cover checks; if it fails it is skipped with a note
- [ ] API tests (Aspire testing seam) with tagged fixtures, including a non-square embedded cover
