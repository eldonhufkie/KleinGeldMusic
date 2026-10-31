# 41: Human-only: prepare TrueNAS

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** The owner upgrades TrueNAS and prepares the custom apps that deployments will update. Use `/wizard` to walk through it.

**Blocked by:** None (can start immediately)

**Status:** needs-human

- [ ] TrueNAS upgraded 24.10 → 25.04 → 25.10
- [ ] Verified that a custom app accepts `include:` with an absolute `env_file` path
- [ ] `kleingeld` and `kleingeld-test` dataset folders exist with the custom-app stubs pasted
- [ ] Persistent host paths for PostgreSQL data and Data Protection keys exist
