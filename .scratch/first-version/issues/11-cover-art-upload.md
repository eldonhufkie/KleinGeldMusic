# 11: Cover art upload

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** A managing Member uploads cover art straight to Blob with progress; bad covers are rejected with a clear message.

**Blocked by:** 10 (Draft Release details)

**Status:** ready-for-agent

- [ ] The API hands out a short-lived SAS write link; the browser uploads directly and then confirms
- [ ] JPEG or PNG only, square, at least 1000×1000 px, at most 20 MB; anything else is rejected with a reason
- [ ] The original plus ~300 px and ~1000 px copies are stored
- [ ] Blob names are write-once: replacing a cover writes new blobs, never overwrites
- [ ] Covers are never in a public container
- [ ] API tests (Aspire testing seam) against Azurite for valid, non-square, too-small and wrong-format covers
