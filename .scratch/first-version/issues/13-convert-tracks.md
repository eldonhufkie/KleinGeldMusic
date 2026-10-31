# 13: Convert Tracks

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** Uploaded Tracks are converted in the background to 192 kbps AAC `.m4a` and become Ready; failures show Failed and can be replaced.

**Blocked by:** 12 (Upload a Track)

**Status:** ready-for-agent

- [ ] A conversion worker picks up uploaded Tracks (work state in PostgreSQL, no queue), converts to fast-start AAC ~192 kbps `.m4a`, uploads it under a new blob name and records duration
- [ ] Status goes Converting → Ready, or Failed on error; the original is kept
- [ ] A Failed Track can be removed or its audio replaced
- [ ] API test (Aspire testing seam): an uploaded fixture becomes Ready with a duration; a corrupt fixture becomes Failed
