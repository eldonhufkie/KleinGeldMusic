# 07: Change login email; passkeys and 2FA

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** A Member changes their login email on the Manage pages, confirmed by email 4 to the new address. The template's passkey and 2FA pages work as optional extras.

**Blocked by:** 04 (Approve a Join Request and set a password)

**Status:** ready-for-agent

- [ ] Changing email sends email 4 to the new address; the login email changes only after confirmation
- [ ] Passkey and 2FA pages work and are required for no one, the Admin included
- [ ] API test (Aspire testing seam): change-email flow via MailPit
