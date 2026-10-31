# 06: Expired links, Forgot password, Resend setup email

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** Someone whose link has expired is pointed to Forgot password (email 3) and told the Admin can resend; the Admin has a Resend setup email action.

**Blocked by:** 04 (Approve a Join Request and set a password)

**Status:** ready-for-agent

- [ ] An expired or invalid link page links to Forgot password and mentions the Admin can resend
- [ ] Forgot password sends email 3; its link resets the password (works for approved Members who never set one)
- [ ] The Admin can resend email 1 for a Member who has no password yet; no link is ever shown to the Admin
- [ ] API tests (Aspire testing seam) for reset and resend, including expiry with the fake clock
