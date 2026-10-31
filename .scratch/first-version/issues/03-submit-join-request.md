# 03: Submit a Join Request

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** An outsider fills in name, email and message on the landing page and always sees the same confirmation. The Admin gets email 5.

**Blocked by:** 02 (Admin logs in)

**Status:** ready-for-agent

- [ ] The form validates name, email and message and stores an open Join Request
- [ ] The response is identical whether or not the email belongs to a Member or an open request
- [ ] API test (Aspire testing seam): submitting a Join Request delivers email 5 to the Admin in MailPit
