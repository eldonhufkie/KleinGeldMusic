# Deploying to a TrueNAS custom app from Azure DevOps

Type: research
Status: open
Blocked by: —

## Question

How can a new version of KleinGeld Music get from Azure DevOps to the TrueNAS SCALE (ElectricEel-24.10) custom app that runs Aspire's generated Docker Compose file?

Compare:
- **Container registry**: Azure Container Registry, GitHub Container Registry, Docker Hub, or one on TrueNAS; costs and how TrueNAS pulls from a private registry.
- **Getting the update applied**: a self-hosted Azure Pipelines agent on TrueNAS (or a VM/container on it), SSH from a Microsoft-hosted agent over Tailscale, the TrueNAS API (`app.update` / redeploy), Watchtower-style auto-pull, or a manual "pull and redeploy" button.
- How Aspire's `aspire publish` / `AddDockerComposeEnvironment` output fits each route, including how secrets (connection strings, Gmail app password, Azure storage keys) reach the containers.
- How database migrations (EF Core) run during a deploy.
- What breaks on TrueNAS upgrades (custom apps moved from k3s to Docker in ElectricEel).
