# 46: Test seed data

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** The Test environment starts with an Admin, a couple of Members and a few sample Releases, never a copy of Production.

**Blocked by:** 19 (Approve: Published), 45 (Deploy workflow)

**Status:** ready-for-agent

- [ ] A seed step runs only in Test and is idempotent
- [ ] Seed creates an Admin, two Members, and a few Published Releases with short generated audio
- [ ] Test email goes to its MailPit container
