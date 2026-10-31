# 40: Remove a Member

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** The Admin Removes a Member with a reason; their data is deleted as for Leave, they get email 10, and they are cut off at once.

**Blocked by:** 39 (Leave)

**Status:** ready-for-agent

- [ ] Remove requires a reason and shows the same preview as Leave
- [ ] Email 10 contains the reason and any Artists deleted with them; email 9 is not sent for those Artists
- [ ] Their next request is refused immediately, including play-link requests; outstanding links are revoked
- [ ] The Admin can't be Removed
- [ ] Someone Removed can submit a new Join Request with the same email
- [ ] API tests (Aspire testing seam) for deletion, email 10, immediate refusal and rejoining
