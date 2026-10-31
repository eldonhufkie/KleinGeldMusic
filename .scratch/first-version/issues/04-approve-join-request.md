# 04: Approve a Join Request and set a password

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** The Admin approves a Join Request from the Admin's Join Requests page. A passwordless Member with a confirmed email is created and gets email 1 with a set-password link; they set a password and log in.

**Blocked by:** 03 (Submit a Join Request)

**Status:** ready-for-agent

- [ ] The Admin sees open Join Requests with name, email, message and time
- [ ] Approving creates a Member with the approved email marked confirmed and no password
- [ ] Email 1 contains a set-password link; using it sets the password and the Member can log in
- [ ] All Identity tokens last 15 minutes
- [ ] API test (Aspire testing seam): full flow from Join Request to login, reading the link from MailPit; with the clock moved 16 minutes the link is refused
