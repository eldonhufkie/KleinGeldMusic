# 42: Human-only: Azure, Gmail and secrets

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** The owner creates the cloud accounts and writes the server's secrets. Use `/wizard` to walk through it.

**Blocked by:** None (can start immediately)

**Status:** needs-human

- [ ] Audio storage account with blob and container soft delete (30 days) and CORS for the `ts.net` address(es)
- [ ] Separate backup storage account in another region: Cool tier, container with 30-day immutability and a 30-day lifecycle delete
- [ ] Dedicated Gmail account with 2-Step Verification and an app password
- [ ] Root-only `secrets.env` (mode 600) written for Production and Test, including the audio account key
- [ ] Password manager holds copies of the secrets
