# 05: Reject a Join Request, with flags

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** The Admin rejects a Join Request; the person gets a short, polite email 2 with no reason. Requests from an existing Member's email or duplicating an open request are flagged and rejectable in one click.

**Blocked by:** 03 (Submit a Join Request)

**Status:** ready-for-agent

- [ ] Rejecting sends email 2 with no reason
- [ ] Requests from an existing Member's email are flagged
- [ ] A request duplicating an open request's email is flagged
- [ ] Flagged requests have a one-click reject
- [ ] API tests (Aspire testing seam) for each flag and for the email
