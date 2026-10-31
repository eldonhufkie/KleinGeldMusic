# 32: Admin rejects a Published Release, or deletes with a reason

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** The Admin Rejects a Published Release with a reason, or deletes any Release or Artist with a reason that is emailed to its managers.

**Blocked by:** 20 (Reject and resubmit), 30 (Takedown and play-link revocation)

**Status:** ready-for-agent

- [ ] Rejecting a Published Release needs a reason; it becomes Rejected, email 8 is sent, and links are revoked
- [ ] Admin delete of a Release or Artist needs a reason and sends email 9 to the managing Members
- [ ] The Admin never does a Takedown and has no Playlist access
- [ ] API tests (Aspire testing seam) for both actions and both emails
