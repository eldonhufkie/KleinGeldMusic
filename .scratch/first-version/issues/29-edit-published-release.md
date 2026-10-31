# 29: Edit a Published Release without review

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** A managing Member edits the title, cover, description, release date, genre, Release type or Track details of a Published Release, and the change is live at once.

**Blocked by:** 19 (Approve: Published)

**Status:** ready-for-agent

- [ ] These edits apply immediately with no review
- [ ] Renaming corrects the slug; old URLs redirect
- [ ] API tests (Aspire testing seam): an edit is visible to other Members at once and the stage stays Published
