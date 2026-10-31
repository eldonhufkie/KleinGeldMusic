# 14: Clean up unattached uploads

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** A daily job deletes uploaded blobs that never got attached to anything, once they are older than 24 hours.

**Blocked by:** 12 (Upload a Track)

**Status:** ready-for-agent

- [ ] Blobs not referenced by any Track or Release and older than 24 h are deleted daily
- [ ] Attached blobs and recent unattached ones are kept
- [ ] API test (Aspire testing seam): with the fake clock advanced past 24 h the orphan is gone and attached blobs remain
