# 20: Reject and resubmit

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** The Admin rejects a Submitted Release with a required reason (email 8). The Artist edits it like a Draft with the reason shown and resubmits with a new Rights Declaration.

**Blocked by:** 18 (Submit, Withdraw, Review page)

**Status:** ready-for-agent

- [ ] Rejecting requires a free-text reason, shown on My Artists with a Rejected badge
- [ ] Email 8 with the reason goes to the managing Members (not the Admin for their own Artists)
- [ ] A Rejected Release is editable like a Draft and stays Rejected until resubmitted
- [ ] Resubmitting requires a new Rights Declaration
- [ ] API tests (Aspire testing seam) for reject, edit and resubmit
