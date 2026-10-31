# 09: Artist managers: add and Stop managing

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** A managing Member adds another Member as a manager by picking from a list; any manager can make any manager (themselves included) Stop managing; the last manager can't.

**Blocked by:** 04 (Approve a Join Request and set a password), 08 (Create an Artist; My Artists page)

**Status:** ready-for-agent

- [ ] Managers pick any Member from a list and add them at once (no invitation, no email)
- [ ] Any manager can make any manager Stop managing
- [ ] Stop managing is refused for the last manager, with a message to delete the Artist instead
- [ ] The Admin has no special control over managers
- [ ] API tests (Aspire testing seam) for each rule
