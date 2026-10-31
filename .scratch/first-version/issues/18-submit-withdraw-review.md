# 18: Submit, Withdraw, Review page

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** A managing Member submits a complete Draft with a Rights Declaration; it's locked until the Admin reviews it or the Artist Withdraws it. The Admin sees it on a Review page with a count badge and gets email 6.

**Blocked by:** 11 (Cover art upload), 13 (Convert Tracks)

**Status:** ready-for-agent

- [ ] Submit is disabled until every Track is Ready, the cover is set and the Release has 1–50 Tracks
- [ ] The Rights Declaration checkbox is required; each submission stores Member, time and wording version
- [ ] A Submitted Release can't be edited; Withdraw returns it to Draft
- [ ] The Review page lists Submitted Releases oldest first; the Admin link shows the count
- [ ] Email 6 goes to the Admin, except for the Admin's own Artists
- [ ] API tests (Aspire testing seam) for each rule, the badge count and email 6 (including the Admin's own-Artist exception)
