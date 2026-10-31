# Deploying to a TrueNAS custom app from Azure DevOps

Type: research
Status: resolved
Blocked by: —

## Question

How can a new version of KleinGeld Music get from Azure DevOps to the TrueNAS SCALE (ElectricEel-24.10) custom app that runs Aspire's generated Docker Compose file?

Compare:
- **Container registry**: Azure Container Registry, GitHub Container Registry, Docker Hub, or one on TrueNAS; costs and how TrueNAS pulls from a private registry.
- **Getting the update applied**: a self-hosted Azure Pipelines agent on TrueNAS (or a VM/container on it), SSH from a Microsoft-hosted agent over Tailscale, the TrueNAS API (`app.update` / redeploy), Watchtower-style auto-pull, or a manual "pull and redeploy" button.
- How Aspire's `aspire publish` / `AddDockerComposeEnvironment` output fits each route, including how secrets (connection strings, Gmail app password, Azure storage keys) reach the containers.
- How database migrations (EF Core) run during a deploy.
- What breaks on TrueNAS upgrades (custom apps moved from k3s to Docker in ElectricEel).

## Answer

- **TrueNAS version first**: 24.10 is archived; the upgrade path is 24.10.2.4 → 25.04.2.6 → 25.10.7. 24.10 can't store private-registry credentials (Docker Registries / `app.registry.*` arrive in 25.04); a private image can only be pulled one-off with credentials (UI Pull Image or `app.image.pull` with `auth_config`). Upgrades also change deploy-relevant things: REST API deprecated in 25.04 (removed in 26), API keys tied to users and HTTPS-only, compose run with `--config /etc/docker` from 25.04.
- **Registries**: ACR Basic ~$5/mo (pull-only scoped tokens); GHCR $0 today (one month's notice before billing); Docker Hub free allows only 1 private repo (Aspire builds one image per project); a registry on TrueNAS is free but pushes from Azure need TLS (`tailscale cert`, manual 90-day renewal) and a CI machine on the tailnet.
- **Applying an update** (none needs open router ports): a self-hosted Azure Pipelines agent on TrueNAS (outbound only, but `docker.sock` access is root on the host); a Microsoft-hosted agent joining the tailnet as an ephemeral tagged node via OAuth, then OpenSSH + `midclt` (script step, no official task; free tier 1,800 min/month); the TrueNAS JSON-RPC API (`app.pull_images` / `app.redeploy` / `app.update`, names differ by version); Watchtower is archived (Dec 2025) and bypasses TrueNAS; manual Pull Image + redeploy always works as a fallback.
- **Aspire output**: `aspire publish` writes a compose file with `${VAR}` placeholders and an unfilled `.env`; TrueNAS's YAML editor has no `.env` support. Workarounds: a custom-app YAML that only `include:`s the compose file with an `env_file` on a dataset, or render with `docker compose config` in CI and send via `app.update` (secrets then visible in the TrueNAS edit screen).
- **Secrets** reach containers as environment variables; the pipeline can write them to a root-only env file on the dataset. Treat `.env.Production` as plain text.
- **Startup order**: Aspire maps `WaitForCompletion` to `service_completed_successfully` but `WaitFor` only to `service_started`, so the API and migrator need connection retries against Postgres.
- **Migrations**: EF Core recommends a migration bundle run once as a job. Options: Aspire's `AddEFMigrations().PublishAsMigrationBundle(...)` (prerelease only), a migrator worker the API waits on with `WaitForCompletion`, or `MigrateAsync()` at startup (acceptable for one container with EF 9+ migration locking).
- **Gaps to verify on the box**: `include:` with an absolute `env_file` on 24.10; whether secret parameters are plain text in `.env.Production`; whether a root `docker login` is used by `app.pull_images` and survives upgrades; Tailscale plan limits for tagged/ephemeral devices; how the TrueNAS Tailscale app is set up for Funnel.

Findings, with citations: `docs/research/deploy-to-truenas.md` on branch `research/deploy-to-truenas` (pushed to origin; never merged).
