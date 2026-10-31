# Deploying from Azure DevOps to a TrueNAS SCALE custom app

Research for ticket `15-deploy-to-truenas-research`. Sources read on **2026-10-31**. The target box runs **TrueNAS SCALE 24.10.2.4 (ElectricEel)**. The app is published through Tailscale Funnel, and the router has no open ports. Prices exclude VAT.

Every claim links to its source. Lines marked **(inference)** are conclusions drawn from the sources, not statements the sources make. Verify those on the box before relying on them.

## Answer

Every working route has two parts. The images go to a registry. Then something on the home side, or something that can reach it over Tailscale, tells TrueNAS to use the new tag. The facts that most limit the choice:

1. **24.10.2.4 cannot store private-registry credentials.** The *Docker Registries* screen and the `app.registry.*` API first appear in 25.04 ([25.04 Apps UI reference](https://www.truenas.com/docs/scale/25.04/scaleuireference/apps/), [API v25.04 `app.registry.create`](https://api.truenas.com/v25.04/api_methods_app.registry.create.html)). The 24.10.2.4 middleware has no registry plugin ([middleware tree at release/24.10.2.4](https://github.com/truenas/middleware/tree/release/24.10.2.4/src/middlewared/middlewared/plugins/apps)). On 24.10 a private image is pulled once, with credentials passed only to that pull (`app.image.pull` with `auth_config`) ([images.py @24.10.2.4](https://github.com/truenas/middleware/blob/release/24.10.2.4/src/middlewared/middlewared/plugins/apps_images/images.py)).
2. **24.10 is archived.** The supported path is 24.10.2.4 → 25.04.2.6 → 25.10.7. Only 25.10 is recommended for General and Mission Critical users ([Software Status](https://www.truenas.com/docs/softwarestatus/)). Any route you design now goes through at least two major TrueNAS upgrades.
3. **The Aspire output is not directly pasteable into "Install via YAML".** `aspire publish` writes `docker-compose.yaml` with `${VAR}` placeholders, plus a `.env` file ([aspire.dev: Docker Compose](https://aspire.dev/deployment/docker-compose/), [deploy first app](https://aspire.dev/get-started/deploy-first-app/)). TrueNAS's YAML editor has no `.env` support. Compose's `include:` with `env_file:` is the documented way around this, covered below.
4. **Nothing has to listen on the internet.** A self-hosted agent only makes outbound long-poll HTTPS calls ([Azure Pipelines agents](https://learn.microsoft.com/en-us/azure/devops/pipelines/agents/agents?view=azure-devops)). A Microsoft-hosted agent can join the tailnet as an ephemeral, tagged node ([Tailscale OAuth clients](https://tailscale.com/kb/1215/oauth-clients)).

| Route | Inbound exposure | Works on 24.10.2.4 with a private registry? | Moving parts | Survives TrueNAS upgrades |
|---|---|---|---|---|
| A. Self-hosted agent on TrueNAS (container) | None, outbound only | Yes. The agent can `docker login` and pull itself, or call `app.image.pull` with `auth_config` | Agent container, PAT or service principal, Docker socket or `midclt` access | Medium: an app like any other; the docker.sock mount is root-equivalent |
| B. Microsoft-hosted agent + Tailscale + SSH | Tailnet only (SSH on TrueNAS, ACL-restricted) | Yes, through `midclt call -job app.image.pull …` or `docker login` over SSH | OAuth client, tag, ACL, SSH key or Tailscale SSH, TrueNAS user | Good: SSH and `midclt` are stable; method names change between API versions |
| C. TrueNAS JSON-RPC API (`app.pull_images`, `app.update`, `app.redeploy`) | Tailnet only (TrueNAS web UI port) | Partly: `app.pull_images` has no credential argument on 24.10 | API key, websocket client | Medium: REST deprecated in 25.04, removed in 26; the API is versioned |
| D. Watchtower-style auto-pull | None | Yes, if it gets a `config.json` | One container, socket mount | Poor: Watchtower is archived (Dec 2025) and it changes containers behind TrueNAS's back |
| E. Manual: UI "Pull Image" + edit YAML / Update | None | Yes (UI Pull Image has optional credentials) | Human | Good |

| Registry | Cost at this scale | Private images | How TrueNAS 24.10 pulls it | Notes |
|---|---|---|---|---|
| Azure Container Registry, Basic | $0.1666/day ≈ **$5/month**, 10 GiB included, then $0.10/GB-month ([Retail Prices API](https://prices.azure.com/api/retail/prices?$filter=serviceName%20eq%20%27Container%20Registry%27%20and%20armRegionName%20eq%20%27westeurope%27%20and%20priceType%20eq%20%27Consumption%27), [ACR SKUs](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-skus)) | Yes; no anonymous pull on Basic | Pull-only repository-scoped token as username/password ([ACR SKUs](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-skus)) | Same tenant as Blob Storage; native Azure Pipelines service connection |
| GitHub Container Registry | **$0 today**: "Container image storage and bandwidth for the Container registry is currently free" ([GitHub Packages billing](https://docs.github.com/en/billing/concepts/product-billing/github-packages)) | Yes | PAT (`read:packages`) as password | Needs a GitHub account; GitHub promises one month's notice before billing changes |
| Docker Hub | Personal $0 with **1 private repo**; Pro $9–11/user/month for unlimited ([Docker pricing](https://www.docker.com/pricing/)) | 1 repo on free | Access token as password | Aspire produces one image per project, so the free tier does not fit without packing every image into one repo as tags |
| Registry on TrueNAS (`registry:3`, CNCF Distribution) | $0 plus disk | Yes | `localhost:<port>` pulls need no TLS (127.0.0.0/8 is insecure-by-default, [dockerd](https://docs.docker.com/reference/cli/dockerd/)) | Pushes from Azure need TLS ([Distribution: deploying](https://distribution.github.io/distribution/about/deploying/)). That means `tailscale cert` (90-day renewal is yours) ([Tailscale HTTPS](https://tailscale.com/kb/1153/enabling-https)), and the pusher must be on the tailnet |

## 1. What Aspire's Docker Compose publish produces

- `builder.AddDockerComposeEnvironment("env")` makes every resource a Compose service ([aspire.dev: Docker Compose](https://aspire.dev/deployment/docker-compose/)).
- `aspire publish` writes `aspire-output/docker-compose.yaml` and a `.env` that lists the expected parameters **unfilled**. It does not build images ([same page](https://aspire.dev/deployment/docker-compose/)).
- `aspire do prepare-<env-resource> --environment Production` builds the images and writes `.env.Production` with values filled in ([same page](https://aspire.dev/deployment/docker-compose/)).
- `aspire deploy` runs prepare and then `docker compose up -d --remove-orphans` against the local Docker ([same page](https://aspire.dev/deployment/docker-compose/)). The docs do not cover a remote host.
- Project images are referenced by variable. Example generated output: `image: "${APISERVICE_IMAGE}"` in YAML, and `APISERVICE_IMAGE=apiservice:latest` and `APISERVICE_PORT=8080` in `.env.Production` ([deploy first app](https://aspire.dev/get-started/deploy-first-app/)).
- **Pushing:** `AddContainerRegistry(...)` plus `WithContainerRegistry(...)` (experimental, `ASPIRECOMPUTE003`), then `aspire do push` after `docker login`. The endpoint and repository can be parameters (`REGISTRY_ENDPOINT`, `REGISTRY_REPOSITORY`). `WithRemoteImageName`, `WithRemoteImageTag` and `WithImagePushOptions` set the tag, for example from `APP_VERSION` ([same page](https://aspire.dev/deployment/docker-compose/)). The docs' CI example is GitHub Actions to GHCR. In Azure Pipelines it is the same shell commands.
- **Parameters and secrets:** `AddParameter("x")` reads `Parameters:x`. In CI, set `Parameters__x` environment variables, which take the highest priority ([aspire.dev: Environments](https://aspire.dev/deployment/environments/)). `AsEnvironmentPlaceholder` keeps a parameter as a `${...}` placeholder in YAML and `.env`, and `ConfigureEnvFile` edits captured variables ([Docker Compose page](https://aspire.dev/deployment/docker-compose/)). The docs do not say whether secret parameters are masked. Assume `.env.Production` from prepare holds them **in plain text (inference)**, so treat it as a secret file and never commit it.
- **Startup ordering in the generated YAML:** `WaitForCompletion` becomes `depends_on: condition: service_completed_successfully`. **Every other wait type, including `WaitFor`, becomes `service_started`.** The `service_healthy` mapping is commented out in the source ([DockerComposeServiceResource.cs](https://github.com/dotnet/aspire/blob/main/src/Aspire.Hosting.Docker/DockerComposeServiceResource.cs)). So `WaitFor(postgres)` does **not** wait for Postgres to accept connections under Compose. The API needs connection retries, or a migrator gate (section 3).
- Useful hooks: `ConfigureComposeFile`, and `PublishAsDockerComposeService` (restart policy, labels, container name). Image pull policy maps to Compose `pull_policy` ([Docker Compose page](https://aspire.dev/deployment/docker-compose/)). `WithDashboard(enabled: false)` drops the Aspire dashboard service ([Docker integration](https://aspire.dev/integrations/compute/docker/)).

### Fitting it into a TrueNAS custom app

TrueNAS's *Install via YAML* screen has only a name and a "Custom Config" YAML box ([24.10 Install Custom App screens](https://www.truenas.com/docs/scale/24.10/scaleuireference/apps/installcustomappscreens/)). The file may start with `include:`, which the docs describe as a way to reference "an external Compose file that defines services" ([apps.truenas.com: custom apps](https://apps.truenas.com/managing-apps/installing-custom-apps/)). Compose's long-form `include` takes `path`, `project_directory` and `env_file`. The `env_file` "defines an environment file(s) to use to define default values when interpolating variables" ([Compose `include` reference](https://docs.docker.com/reference/compose-file/include/)). The pieces fit like this:

```yaml
# TrueNAS "Install via YAML" → Custom Config (whole file)
include:
  - path: /mnt/<pool>/apps/kleingeld/docker-compose.yaml
    env_file: /mnt/<pool>/apps/kleingeld/.env.Production
```

A deploy then means three steps: write the new `docker-compose.yaml` and `.env.Production` (with new `*_IMAGE` tags) to the dataset, make sure the images are present locally, and ask TrueNAS to re-run compose. `app.update` re-runs `docker compose up --force-recreate --remove-orphans` unless the app is stopped ([crud.py @24.10.2.4](https://github.com/truenas/middleware/blob/release/24.10.2.4/src/middlewared/middlewared/plugins/apps/crud.py)), and so does `app.redeploy` ([app_scale.py @24.10.2.4](https://github.com/truenas/middleware/blob/release/24.10.2.4/src/middlewared/middlewared/plugins/apps/app_scale.py)). TrueNAS docs never mention `include` for 24.10 specifically. Docker Engine on 24.10 is 27.5.0 ([24.10 release notes](https://www.truenas.com/docs/scale/24.10/gettingstarted/scalereleasenotes/)), and that bundles a Compose v2 that supports `include` **(inference: test it once)**.

Alternative: run `docker compose --env-file .env.Production config` in CI to render one fully interpolated YAML, and push that through `app.update` as `custom_compose_config_string` ([interpolation docs](https://docs.docker.com/compose/how-tos/environment-variables/variable-interpolation/), [API v25.10 `app.update`](https://api.truenas.com/v25.10/api_methods_app.update.html)). The secrets then sit inside the app config stored by TrueNAS and are visible in the Edit YAML screen **(inference)**.

### How the secrets reach the containers

Aspire passes connection strings and other parameters as environment variables in the generated compose (`ConnectionStrings__db`, `Parameters__...`-derived values). On TrueNAS they can come from:

| Mechanism | Where the secret lives | Notes |
|---|---|---|
| `.env.Production` next to the included compose file (above) | ZFS dataset, file mode 600, owned by root | Simplest fit with Aspire's output. The pipeline writes it from Azure DevOps secret variables, or a variable group linked to Key Vault |
| Fully rendered YAML via `app.update` | TrueNAS app config | One artifact, but secrets end up in the UI |
| Hand-edited `.env` on the box, CI writes only image tags | Dataset | Secrets never leave home; CI changes only `*_IMAGE` values (for example via a second `env_file`; later files override earlier ones, [interpolation docs](https://docs.docker.com/compose/how-tos/environment-variables/variable-interpolation/)) |

For this app that covers the Postgres password and connection string, the Gmail app password, and the Azure Storage connection string or account key. The Azure Storage credential can't use managed identity off-Azure, so a SAS or account key in the env file is the practical choice **(inference)**.

## 2. Ways to apply the update

### A. Self-hosted Azure Pipelines agent on TrueNAS

- Microsoft documents a Linux agent image driven by `AZP_URL`, `AZP_TOKEN` (or `AZP_CLIENTID`, `AZP_CLIENTSECRET` and `AZP_TENANTID` for a service principal), `AZP_POOL` and `AZP_AGENT_NAME`. The token needs **Agent Pools (read, manage)** ([agent in Docker](https://learn.microsoft.com/en-us/azure/devops/pipelines/agents/docker)).
- The agent finds jobs through "an HTTP long poll", so it needs no inbound ports ([agents](https://learn.microsoft.com/en-us/azure/devops/pipelines/agents/agents?view=azure-devops)).
- Cost: one self-hosted parallel job is free for private projects, with no time limit ([parallel jobs](https://learn.microsoft.com/en-us/azure/devops/pipelines/licensing/concurrent-jobs)).
- To touch the host's Docker, you bind-mount `/var/run/docker.sock`. Microsoft warns: "Doing this has serious security implications. The code inside the container can now run as root on your Docker host" ([agent in Docker](https://learn.microsoft.com/en-us/azure/devops/pipelines/agents/docker)). Running the agent as a TrueNAS custom app works. A small VM on the box is the more isolated choice.
- Build and push can stay on a Microsoft-hosted agent, with only a deploy stage on the home agent. Or the home agent can do everything (.NET 10 SDK and Aspire CLI in the image).

### B. Microsoft-hosted agent joins the tailnet, then SSH

- Tailscale documents a GitHub Action, but no Azure Pipelines task ([GitHub Action](https://tailscale.com/kb/1276/tailscale-github-action)). The generic mechanism is an **OAuth client** with the `auth_keys` scope and assigned tags, used directly as an auth key: `tailscale up --auth-key=${OAUTH_CLIENT_SECRET} --advertise-tags=tag:ci`. Options `ephemeral` (default `true`) and `preauthorized` can be appended URL-style ([OAuth clients](https://tailscale.com/kb/1215/oauth-clients)).
- Ephemeral nodes are removed 30–60 minutes after last activity, or immediately on `tailscale logout`. End the job with `tailscale logout` ([ephemeral nodes](https://tailscale.com/kb/1111/ephemeral-nodes)).
- SSH: Tailscale SSH allows tagged→tagged connections only with `accept`, because check mode isn't allowed from tagged sources ([Tailscale SSH](https://tailscale.com/kb/1193/tailscale-ssh)). On TrueNAS, Tailscale normally runs as the community **Tailscale app** ([TrueNAS Tailscale app](https://apps.truenas.com/resources/deploy-tailscale/)). Tailscale SSH would then land inside that container, not on the host **(inference)**. The practical target is TrueNAS's own OpenSSH service on the tailnet address, with a key-only deploy user and an ACL that allows only `tag:ci` → TrueNAS:22 **(inference)**.
- On the box: write the files, then `midclt call -job app.image.pull '{"image":"…","auth_config":{"username":"…","password":"…"}}'` and `midclt call -job app.redeploy <app>` (methods from the 24.10.2.4 source above). The 25.04 notes name `midclt` as the supported CLI client ([25.04 release notes](https://www.truenas.com/docs/scale/25.04/gettingstarted/scalereleasenotes/)).
- Cost: on the Microsoft-hosted free tier, one job of up to 60 min, 1,800 min/month ([parallel jobs](https://learn.microsoft.com/en-us/azure/devops/pipelines/licensing/concurrent-jobs)). Tailscale Funnel and OAuth are available on all plans ([Funnel](https://tailscale.com/kb/1223/funnel)). Plan-level device limits weren't checked.

### C. TrueNAS API

- 25.04+ uses a versioned JSON-RPC 2.0 WebSocket API. REST was deprecated in 25.04 ([25.04 release notes](https://www.truenas.com/docs/scale/25.04/gettingstarted/scalereleasenotes/)), and "Full removal is planned for TrueNAS 26" ([25.10 version notes](https://www.truenas.com/docs/scale/25.10/gettingstarted/versionnotes/)). API keys are user-linked, must go over HTTPS (keys sent over plain HTTP are revoked), and allow-list legacy keys are revoked on upgrade ([25.04 release notes](https://www.truenas.com/docs/scale/25.04/gettingstarted/scalereleasenotes/)).
- Relevant jobs: `app.pull_images(app_name, {redeploy: true})` pulls and redeploys ([v25.10](https://api.truenas.com/v25.10/api_methods_app.pull_images.html); also present in [24.10.2.4](https://github.com/truenas/middleware/blob/release/24.10.2.4/src/middlewared/middlewared/plugins/apps/pull_images.py)). `app.redeploy` stops, pulls the latest images and restarts ([v25.10](https://api.truenas.com/v25.10/api_methods_app.redeploy.html)). `app.update` accepts `custom_compose_config_string` ([v25.10](https://api.truenas.com/v25.10/api_methods_app.update.html)). All require `APPS_WRITE`.
- On 24.10, `app.pull_images` runs plain `docker compose pull` with no credentials of its own ([compose_utils.py @24.10.2.4](https://github.com/truenas/middleware/blob/release/24.10.2.4/src/middlewared/middlewared/plugins/apps/compose_utils.py)). For a private registry, either pre-pull with `app.image.pull` and its `auth_config`, or rely on root's Docker credentials. From 25.04, compose runs with `docker --config /etc/docker` ([compose_utils.py @25.04.2.6](https://github.com/truenas/middleware/blob/release/25.04.2.6/src/middlewared/middlewared/plugins/apps/compose_utils.py)), which is presumably where the Docker Registries entries are written **(inference)**. So a `docker login` done by hand on 24.10 may silently stop applying after the upgrade.
- The API is reachable only via the tailnet here, so route C still needs B's tailnet join. It saves the SSH user, but you write a WebSocket client.

### D. Watchtower-style auto-pull

`containrrr/watchtower` was **archived on 2025-12-17**. Its README says "This project is no longer maintained" and does not recommend it for production ([GitHub](https://github.com/containrrr/watchtower)). It also recreates containers outside TrueNAS's middleware, so the app state TrueNAS shows can drift from what is running **(inference)**. A pull-based alternative inside TrueNAS: leave "Check for docker image updates" on ([24.10 Apps UI reference](https://www.truenas.com/docs/scale/24.10/scaleuireference/apps/)), use a moving tag, and click Update. Update detection for custom apps was broken in 24.10.0, with a fix targeted for 24.10.1 ([24.10 release notes](https://www.truenas.com/docs/scale/24.10/gettingstarted/scalereleasenotes/)). Moving tags also make migrations and rollback harder (section 3).

### E. Manual

UI: **Apps → Configuration → Manage Container Images → Pull Image**, with optional registry username and password ([24.10 Apps UI reference](https://www.truenas.com/docs/scale/24.10/scaleuireference/apps/)). Then edit the app YAML or `.env` and save. This needs no CI-to-home connectivity at all, and it is a fine fallback for any route above.

## 3. EF Core migrations during a deploy

- EF Core's guidance: "For automated deployment, use a migration bundle". Run it "as a one-shot deployment job after the database is healthy". Don't make every replica migrate from its entrypoint, and "Configure the deployment platform not to restart the migration container after it exits successfully" ([Applying migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)). Bundles use EF 9+ migration locking. Keep the production connection out of the bundle and use a deployment identity with schema rights ([same page](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)).
- **Aspire's `AddEFMigrations`** (package `Aspire.Hosting.EntityFrameworkCore`): `RunDatabaseUpdateOnStart()` applies only to `aspire run` and "has no effect during aspire publish or deployment". `PublishAsMigrationBundle(publishContainer: true)` turns the migrations into a container that each environment deploys. For Compose, the docs add `.PublishAsDockerComposeService((_, s) => s.Restart = "no")` ([aspire.dev: EF Core migrations](https://aspire.dev/integrations/databases/efcore/migrations/)). The package is **prerelease only** (latest 13.6.4-preview, 2026-10-24) ([NuGet](https://www.nuget.org/packages/Aspire.Hosting.EntityFrameworkCore)).
- **Migrator worker project** (older sample): a worker applies migrations and exits, and the API uses `WaitForCompletion(migrationService)` ([sample](https://aspire.dev/reference/samples/database-migrations/)). Under Compose that becomes `service_completed_successfully` ([source](https://github.com/dotnet/aspire/blob/main/src/Aspire.Hosting.Docker/DockerComposeServiceResource.cs)), so the API starts only after a successful migration. The migrator's own `WaitFor(db)` becomes only `service_started`, so the migrator must retry until Postgres accepts connections **(inference from the mapping)**.
- **Runtime `MigrateAsync()` at API startup** is allowed with EF 9+ locking. It costs least-privilege and review ([Applying migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)). With one API container that cost is small.
- In every route, the migrator is just another service in the same compose project. TrueNAS recreating the project (`up --force-recreate`) re-runs it, and bundles are idempotent ([Aspire EF migrations](https://aspire.dev/integrations/databases/efcore/migrations/)). Add `dotnet ef migrations has-pending-model-changes` to CI, because EF 9+ throws at migrate time when the model has unmigrated changes ([Applying migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)).

## 4. What breaks on TrueNAS upgrades

**24.04 → 24.10 (already behind this box).** Apps moved from Kubernetes to Docker. Migration covered catalog and UI-custom apps only, and data lives in a hidden dataset at `/mnt/.ix-apps`. Apps did not migrate from encrypted pools, nor when network settings depended on an app container ([24.10 release notes](https://www.truenas.com/docs/scale/24.10/gettingstarted/scalereleasenotes/)). Hand-installed Docker on 24.04 or earlier breaks 24.10+ apps (NAS-134660). Apps on 24.10.2.1 and earlier stopped getting updates after 2025-06-01 ([25.04 release notes](https://www.truenas.com/docs/scale/25.04/gettingstarted/scalereleasenotes/)).

**24.10 → 25.04 (Fangtooth).**
- The REST API is deprecated and JSON-RPC WebSocket is the replacement.
- API keys become user-linked and HTTPS-only. Allow-list keys are revoked.
- Docker Registries and `app.registry` arrive.
- Compose switches to `--config /etc/docker` (source above).
- Custom apps with TTY enabled don't show logs in the UI.
- Built-in Netdata is removed.
- Auxiliary parameters are recommended to be removed before upgrading. SSH auxiliary parameters "can prevent the SSH service from starting", which matters for route B.
- Updating by any method other than the web UI "can make the system inoperable".

All from the [25.04 release notes](https://www.truenas.com/docs/scale/25.04/gettingstarted/scalereleasenotes/).

**25.04 → 25.10 (Goldeye).**
- REST removal is planned for TrueNAS 26.
- Automatic app migration when changing the apps pool, and registry mirrors, are added.
- The Docker start timeout was raised after slow-HDD systems marked apps FAILED.
- Apps on SMB/NFS storage can show "crashed" after boot.
- Several app upgrade/validation bugs were fixed in 25.10.1.
- NVIDIA moves to open drivers, dropping Pascal, Maxwell and Volta.
- 25.10.6 fixes one unreachable registry stopping all update checks.

All from the [25.10 version notes](https://www.truenas.com/docs/scale/25.10/gettingstarted/versionnotes/).

**Practical implications for this design (inference):**
- Keep everything the deploy depends on in **your own dataset**: compose file, env files, Postgres data as host paths. Don't keep it in ix-volumes or in Docker named volumes (the custom-apps guide also recommends bind mounts to TrueNAS storage for production, [apps.truenas.com](https://apps.truenas.com/managing-apps/installing-custom-apps/)).
- Treat API method names and auth as version-specific. Prefer `midclt` over SSH, or the documented UI paths.
- Re-test the deploy after each major upgrade, especially registry auth (24.10's ad-hoc auth vs 25.04's Docker Registries).

## Gaps and things to verify on the box

- `include:` + absolute `env_file:` under TrueNAS 24.10's custom-app validator: documented for TrueNAS generally, not tested on 24.10.
- Whether secret Aspire parameters land in `.env.Production` in plain text. Run `aspire do prepare-…` once and inspect the output.
- Whether a root `docker login` on 24.10 is honoured by `app.pull_images`, and whether it survives the 25.04 upgrade.
- Tailscale plan limits for tagged or ephemeral devices; no Tailscale-provided Azure Pipelines task exists, so CI uses the install script plus `tailscale up`.
- How the community TrueNAS Tailscale app is configured for Funnel and host networking. Its docs page doesn't say ([TrueNAS Tailscale app](https://apps.truenas.com/resources/deploy-tailscale/)). Funnel's ports are limited to 443, 8443 and 10000, and its bandwidth limits are non-configurable ([Funnel](https://tailscale.com/kb/1223/funnel)).
- The 24.10 API reference on api.truenas.com lists events only. The 24.10 method behaviour above comes from the middleware source at tag `release/24.10.2.4`, not from docs.
