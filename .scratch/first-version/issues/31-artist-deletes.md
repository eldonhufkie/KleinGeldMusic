# 31: Artist deletes a Release or the Artist

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** A managing Member permanently deletes a Release, or the whole Artist with all its Releases.

**Blocked by:** 30 (Takedown and play-link revocation)

**Status:** ready-for-agent

- [ ] Delete needs a confirmation, no reason, and sends no email
- [ ] Deleting removes database rows and all blobs; deleting an Artist deletes all its Releases
- [ ] Outstanding play links are revoked by policy rotation
- [ ] API tests (Aspire testing seam): data and blobs are gone; other Members get "not found"
