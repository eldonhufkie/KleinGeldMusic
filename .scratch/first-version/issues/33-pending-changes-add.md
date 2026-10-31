# 33: Pending Changes: add Tracks

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** A managing Member adds Tracks to a Published Release. They wait as Pending Changes while Members keep seeing the old version; the Admin reviews only the added Tracks and approves.

**Blocked by:** 13 (Convert Tracks), 19 (Approve: Published)

**Status:** ready-for-agent

- [ ] Adding Tracks to a Published Release creates its one set of Pending Changes (Draft)
- [ ] Members see and hear only the live version; added Tracks are playable only by managers and the Admin
- [ ] Submitting Pending Changes needs a Rights Declaration and appears on the Review page and badge; email 6 as for Releases
- [ ] The Admin's review shows only what changed; approving merges the Tracks into the Release (email 7)
- [ ] Pending Changes are shown on My Artists
- [ ] API tests (Aspire testing seam) for visibility before and after approval
