# 10: Draft Release details

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** A managing Member starts a New Release as a Draft and fills in title, Release type, release date, description and genre; they can edit or delete it. It shows on My Artists with a Draft badge, and nobody else can see it.

**Blocked by:** 08 (Create an Artist; My Artists page)

**Status:** ready-for-agent

- [ ] Release type is Album, EP or Single and required; title and release date required (past dates allowed); description optional up to ~2,000 chars; genre optional free text
- [ ] All managing Members can edit equally; last write wins
- [ ] My Artists lists the Release with a Draft badge
- [ ] The visibility rule lives in one place: non-Published Releases answer "not found" to everyone but managers and the Admin
- [ ] API tests (Aspire testing seam) for create/edit/delete and for the visibility rule
