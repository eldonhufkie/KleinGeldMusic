# 19: Approve: Published

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** The Admin listens to a Submitted Release on its Release page and approves it; it's Published, every Member can open and play it, and the Artist's managers get email 7.

**Blocked by:** 17 (Play a Track: play link and player bar), 18 (Submit, Withdraw, Review page)

**Status:** ready-for-agent

- [ ] Approval is for the whole Release; it becomes Published with a Published date set on first approval
- [ ] Every Member can open the Release page and get play links for its Tracks
- [ ] Email 7 goes to the Artist's managing Members, except the Admin for their own Artists
- [ ] The Admin may approve their own Releases
- [ ] API tests (Aspire testing seam): before approval other Members get "not found" and refused links; after approval both succeed
