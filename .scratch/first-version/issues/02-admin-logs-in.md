# 02: Admin logs in

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** The seeded Admin logs in with email and password and sees the app shell: sidebar (Home, Admin) and a profile menu with logout. Identity's template pages are used with Register removed.

**Blocked by:** 01 (Walking skeleton)

**Status:** ready-for-agent

- [ ] Exactly one Admin exists, seeded from configuration (a secret parameter in the AppHost)
- [ ] Login with email + password sets a same-origin cookie; logout clears it
- [ ] There is no Register page or endpoint
- [ ] The security stamp is validated on every request, not every 30 minutes
- [ ] API tests (Aspire testing seam): anonymous calls to Member endpoints are refused; the Admin's calls succeed; a changed security stamp refuses the next request
- [ ] The sidebar shows Admin only to the Admin
