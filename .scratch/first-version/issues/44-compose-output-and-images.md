# 44: Compose output and images

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** `aspire publish` produces a compose file ready for TrueNAS, and CI pushes tagged images to GHCR.

**Blocked by:** 01 (Walking skeleton)

**Status:** ready-for-agent

- [ ] All compose changes are made in the AppHost: PostgreSQL `pg_isready` healthcheck, migrator `service_healthy` dependency, API `WaitForCompletion` on the migrator, bind-mounted host paths in publish mode, restart `unless-stopped` (migrator `"no"`)
- [ ] Secrets are AppHost secret parameters; the generated compose reads them from `secrets.env` and the image tag from `release.env`
- [ ] API and migrator use `EnableRetryOnFailure`
- [ ] CI builds and pushes public GHCR images tagged with the commit SHA, never `latest`, keeping the last 10 versions per image
- [ ] The generated compose file is not committed
