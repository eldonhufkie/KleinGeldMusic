# Choose how new versions are deployed

Type: grilling
Status: open
Blocked by: 15

## Question

Using [Deploying to a TrueNAS custom app from Azure DevOps](15-deploy-to-truenas-research.md), decide how a new version is built, stored, and applied on TrueNAS: the container registry, what triggers a deploy (every merge to `develop`, a release branch, a manual button), how secrets are kept, and how EF Core migrations run.

The research also raises: whether to upgrade TrueNAS (24.10 is archived; path 24.10 → 25.04 → 25.10) before the first deploy, since 24.10 can't store private-registry credentials; how Aspire's `.env`-based compose output is fed to a TrueNAS custom app; and how the API waits for Postgres given `WaitFor` maps only to `service_started`.
