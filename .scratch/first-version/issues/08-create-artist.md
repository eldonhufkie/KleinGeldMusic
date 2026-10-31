# 08: Create an Artist; My Artists page

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** Any Member creates an Artist and sees it on a My Artists page that lists the Artist's (empty) Releases and its managers. Ids and URLs follow ADR 0002.

**Blocked by:** 02 (Admin logs in)

**Status:** ready-for-agent

- [ ] A Member creates an Artist with a name and becomes its managing Member
- [ ] Every entity id is a GUID v7; URLs carry id plus slug; an outdated slug redirects to the current one
- [ ] My Artists appears in the sidebar only for Members who manage an Artist
- [ ] The public Artist page isn't reachable while the Artist has no Published Release ("not found")
- [ ] API tests (Aspire testing seam) for creation, slug redirect after rename, and not-found for other Members
