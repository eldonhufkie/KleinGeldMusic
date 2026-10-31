# Deploying KleinGeld Music from Azure DevOps to a TrueNAS custom app

Research for [issue 15](../../.scratch/music-service/issues/15-deploy-to-truenas-research.md), which feeds the decision in [issue 16](../../.scratch/music-service/issues/16-choose-deployment.md). Everything was read on **2026-10-31**. Versions checked: TrueNAS 24.10.2.4 (installed), 25.04.2.x, 25.10.7, and 26-BETA.3; Aspire 13.6.4; EF Core 9+; Docker Engine 27.5 (TrueNAS 24.10/25.04) and 29.0.4 (TrueNAS 26).

Claims come from primary sources: TrueNAS docs, the `truenas/middleware`, `truenas/api_client` and `truenas/apps` source, aspire.dev and the `microsoft/aspire` source, Microsoft Learn, and the GitHub, Docker and Tailscale docs. Where I drew a conclusion from source code rather than from a documented statement, it is marked **(inference)**. Claims with only a community source are marked **(community)**.

## Short answer

**Front-runner:** a **Microsoft-hosted Azure Pipelines agent** does the build. It runs `aspire publish`, builds and pushes the images tagged with the build number to **Azure Container Registry Basic** (about $5/month), and renders the Compose file with the production secrets. It then joins the tailnet with an **ephemeral, tagged Tailscale auth key**, connects to TrueNAS **over SSH**, and runs the local TrueNAS CLI `midclt call -j app.update <app> {custom_compose_config_string: …}`.

TrueNAS stays in charge of the app. The update runs its own `docker compose up --force-recreate`, so the job fails visibly if the stack doesn't come up. EF Core migrations run as a **one-shot migration-bundle container** that the API waits for with `depends_on: service_completed_successfully`.

On **24.10**, TrueNAS has no stored registry credentials for Compose, so the pipeline pre-pulls the private images with `app.image.pull` and an `auth_config`. From **25.04** on, the registry is signed in once under *Apps → Docker Registries*.

Costs:
- ACR Basic: $0.1666/day, about $5.07/month, including 10 GB of storage.
- GHCR: currently $0, the free alternative.
- Azure Pipelines: the free tier covers it (1,800 Microsoft-hosted minutes a month, plus one self-hosted job).
- Tailscale: one extra short-lived device.

Main caveats:
- **24.10 is end of life** and has no way to sign in to a registry. Upgrade to **25.10** before building the pipeline.
- Secrets end up stored in plain text in the TrueNAS app config and in its snapshots.
- Every deploy recreates *all* containers, PostgreSQL included, so there is a short outage.
- The REST API is gone in TrueNAS 26, and `auth.login_with_api_key` is due to be removed in 27. Don't build on either.
- Watchtower is archived and doesn't fit.

---

## 1. Container registry

### Options and costs (read 2026-10-31)

| Registry | Price | Private images | Auth that TrueNAS can use | Notes |
|---|---|---|---|---|
| **Azure Container Registry, Basic** | **$0.1666/day (≈ $5.07/month)**, 10 GB included, extra storage $0.10/GB/month (West Europe, [Azure Retail Prices API](https://prices.azure.com/api/retail/prices?$filter=serviceName%20eq%20%27Container%20Registry%27%20and%20armRegionName%20eq%20%27westeurope%27); the [ACR pricing page](https://azure.microsoft.com/en-us/pricing/details/container-registry/) lists tiers and included storage but rendered prices as "$-") | Yes | **Repository-scoped tokens** (token name plus password, can expire, available "in all the service tiers") used with plain `docker login` ([Microsoft Learn](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-token-based-repository-permissions)) | "Standard networking fees apply to network egress" ([pricing page](https://azure.microsoft.com/en-us/pricing/details/container-registry/)). Azure gives "the first 100 GB/month of egressed data for free" ([bandwidth pricing](https://azure.microsoft.com/en-us/pricing/details/bandwidth/)). Standard tier is $0.6666/day. |
| **GitHub Container Registry (ghcr.io)** | **$0 today**: "Container image storage and bandwidth for the Container registry is currently free", with at least one month's notice before any change ([GitHub billing docs](https://docs.github.com/en/billing/concepts/product-billing/github-packages)) | Yes, private by default ([GHCR docs](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry)) | Only a **classic PAT** (`read:packages` / `write:packages`); fine-grained PATs aren't supported ([GHCR docs](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry)) | The PAT belongs to a person and covers all of that person's packages. The code lives in Azure DevOps, so this adds a second identity provider. |
| **Docker Hub** | Personal $0 (**1 private repo**); Pro $9/user/month annual or $11 monthly ([Docker pricing](https://www.docker.com/pricing/)) | Personal: 1 repo only | Username plus token | Pull limits: 100 per 6 h unauthenticated, 200 per 6 h Personal, unlimited on paid plans ([Docker Hub usage](https://docs.docker.com/docker-hub/usage/)). The pricing page says "100 pulls/hr" for Personal, which conflicts with the usage page. KleinGeld has at least 3–4 images (api, web, conversion job, migrations), so the free plan doesn't fit. |
| **Registry on TrueNAS** (catalog app "Distribution" 3.1.2, [truenas/apps](https://github.com/truenas/apps/tree/master/ix-dev/community/distribution)) | $0 | Yes (`basic_auth_users`, certificate option in [questions.yaml](https://github.com/truenas/apps/blob/master/ix-dev/community/distribution/questions.yaml)) | htpasswd basic auth. TrueNAS only fixed "registries that use htpasswd authentication were not supported" for custom-app updates in **26-BETA.3** ([26 notes](https://www.truenas.com/docs/scale/26/gettingstarted/versionnotes/)) | The Microsoft-hosted agent would have to *push* over the tailnet with TLS. Images live on the same box they protect, so there's no off-box copy. |
| **No registry** (`docker save` → SSH → `docker load`) | $0 | n/a | n/a | Works because Compose's default `pull_policy` is `missing` ([Compose spec](https://docs.docker.com/reference/compose-file/services/)) and TrueNAS's update doesn't force a pull (see §2) **(inference)**. There's no image history off the box, and the full image goes over the tailnet every time. |

### How TrueNAS pulls from a private registry, by version

- **24.10 (installed): there's no persistent registry login for apps.**
  - The 24.10 middleware has no `app_registry` plugin ([plugins at TS-24.10.2.4](https://github.com/truenas/middleware/tree/TS-24.10.2.4/src/middlewared/middlewared/plugins)).
  - Compose runs as plain `docker compose …` with no `--config` ([compose_utils.py @24.10.2.4](https://github.com/truenas/middleware/blob/TS-24.10.2.4/src/middlewared/middlewared/plugins/apps/compose_utils.py)).
  - The only authenticated path is the one-off **Pull Image** dialog / `app.image.pull`, which takes an `auth_config` with `username` and `password` ([images.py @24.10.2.4](https://github.com/truenas/middleware/blob/TS-24.10.2.4/src/middlewared/middlewared/plugins/apps_images/images.py); the [24.10 Apps screen docs](https://www.truenas.com/docs/scale/24.10/scaleuireference/apps/) list "Docker Registry Authentication … Only needed for private images" and no Docker Registries screen).
  - The image-update check is anonymous in 24.10 ([client.py @24.10.2.4](https://github.com/truenas/middleware/blob/TS-24.10.2.4/src/middlewared/middlewared/plugins/apps_images/client.py)), so TrueNAS can't show "update available" for private images **(inference)**.
  - Community reports say that custom apps on private registries only work after a manual pre-pull **(community)** ([forum](https://forums.truenas.com/t/n00b-questions-about-deploying-private-custom-apps/6108)).
  - **Workable on 24.10:** pre-pull with `midclt call -j app.image.pull '{"image": "<acr>.azurecr.io/kleingeld/api:<build>", "auth_config": {...}}'`, then `app.update` with a compose file that names that tag. `compose up` only pulls missing images ([Compose `pull_policy` default](https://docs.docker.com/reference/compose-file/services/)) **(inference)**. Don't use `app.pull_images` here: it runs `compose pull --policy always` without credentials ([pull_images.py](https://github.com/truenas/middleware/blob/TS-24.10.2.4/src/middlewared/middlewared/plugins/apps/pull_images.py), [compose_utils.py](https://github.com/truenas/middleware/blob/TS-24.10.2.4/src/middlewared/middlewared/plugins/apps/compose_utils.py)).
- **25.04 and later: "Sign-in to a Docker registry".**
  - The *Docker Registries* screen ([25.04 Apps docs](https://www.truenas.com/docs/scale/25.04/scaleuireference/apps/)) and the `app.registry.create` API (name, username, password or access token, URI; [API v25.10](https://api.truenas.com/v25.10/api_methods_app.registry.create.html)) arrived with the commit "Allow logging in to different docker registries (#15346)", 2025-01-13 ([compose_utils history](https://github.com/truenas/middleware/commits/master/src/middlewared/middlewared/plugins/apps/compose_utils.py)).
  - The stored credentials are written to `/etc/docker/config.json` ([etc_files/docker/config.json.py @25.04.0](https://github.com/truenas/middleware/blob/TS-25.04.0/src/middlewared/middlewared/etc_files/docker/config.json.py)), and every Compose call now runs `docker --config /etc/docker compose …` ([compose_utils.py @25.04.0](https://github.com/truenas/middleware/blob/TS-25.04.0/src/middlewared/middlewared/plugins/apps/compose_utils.py)).
  - Caveat: 26-BETA.3 still lists the fix "Custom app updates failed when the image came from a registry that requires authentication" (NAS-141149, NAS-141553) ([26 notes](https://www.truenas.com/docs/scale/26/gettingstarted/versionnotes/)). I couldn't confirm whether 25.10.x has this bug or got the fix. Test a private-image update on the version you run.
  - 25.10 also added registry **mirrors** (NAS-136553), and in 25.10.7 a single unreachable registry no longer stops the update check for all apps (NAS-142192) ([25.10 notes](https://www.truenas.com/docs/scale/25.10/gettingstarted/versionnotes/)).

**Registry conclusion:** pick **ACR Basic** if about $5/month is acceptable. You get a pull-only, repository-scoped, expiring token for TrueNAS, and a push credential the pipeline can hold as an Azure DevOps service connection. **GHCR** is the $0 option, at the cost of a broad, person-bound classic PAT. Docker Hub's free plan is too small, and a registry on TrueNAS adds TLS and push-over-tailnet work for little gain.

---

## 2. Ways to apply the update

### What TrueNAS actually does (24.10.2.4 source)

- A custom app's Compose YAML is stored at `/mnt/.ix-apps/app_configs/<app>/versions/<v>/templates/rendered/docker-compose.yaml`, alongside a `user_config.yaml` ([ix_apps/path.py](https://github.com/truenas/middleware/blob/TS-24.10.2.4/src/middlewared/middlewared/plugins/apps/ix_apps/path.py), [state_utils.py](https://github.com/truenas/middleware/blob/TS-24.10.2.4/src/middlewared/middlewared/plugins/docker/state_utils.py)). The Compose project is named `ix-<app>` ([apps/utils.py](https://github.com/truenas/middleware/blob/TS-24.10.2.4/src/middlewared/middlewared/plugins/apps/utils.py)).
- **`app.update <app> {custom_compose_config | custom_compose_config_string}`** validates only that the YAML parses ([custom_app_utils.py](https://github.com/truenas/middleware/blob/TS-24.10.2.4/src/middlewared/middlewared/plugins/apps/custom_app_utils.py)). It saves the YAML and runs `compose down` followed by `compose up -d --force-recreate --remove-orphans`, and raises an error if Compose exits non-zero ([crud.py `update_internal`](https://github.com/truenas/middleware/blob/TS-24.10.2.4/src/middlewared/middlewared/plugins/apps/crud.py), [compose_utils.py](https://github.com/truenas/middleware/blob/TS-24.10.2.4/src/middlewared/middlewared/plugins/apps/compose_utils.py)).- **`app.redeploy <app>`** re-runs the same update with the stored config ([app_scale.py](https://github.com/truenas/middleware/blob/TS-24.10.2.4/src/middlewared/middlewared/plugins/apps/app_scale.py)).
- **`app.pull_images <app> {redeploy: true}`** runs `compose pull --policy always`, then redeploys ([pull_images.py](https://github.com/truenas/middleware/blob/TS-24.10.2.4/src/middlewared/middlewared/plugins/apps/pull_images.py)). This is the right call for a moving tag (for example `:prod`) once a registry login exists (25.04 and later).
- Consequence: **every deploy recreates every container, PostgreSQL included**. Data in volumes survives, but expect a short outage **(inference from `--force-recreate` plus the preceding `down`)**.
- `midclt` ships with TrueNAS. Its `call -j` flag waits for a job to finish; TrueNAS's own docs use `midclt call -j app.update APP_NAME '{…}'` ([25.04 notes](https://www.truenas.com/docs/scale/25.04/gettingstarted/scalereleasenotes/)). The 24.10.2.4 client has `-j`, plus `-u <uri>` and `-K <api key>` for remote use ([api_client @release/24.10.2.4](https://github.com/truenas/api_client/blob/release/24.10.2.4/truenas_api_client/__init__.py)).

### Route A: SSH from a Microsoft-hosted agent over Tailscale, then `midclt` (front-runner)

- **Network:** Tailscale recommends ephemeral nodes for "CI/CD systems". Use an ephemeral, tagged auth key and run `tailscale logout` at the end of the job; inactive ephemeral nodes are otherwise removed after about 30–60 minutes ([Tailscale ephemeral nodes](https://tailscale.com/kb/1111/ephemeral-nodes)).
  - Use ordinary `sshd` with a deploy key, not Tailscale SSH. A Tailscale SSH rule *from a tagged device* can't use check mode, and check mode needs an interactive re-login, which doesn't suit CI ([Tailscale SSH](https://tailscale.com/kb/1193/tailscale-ssh)).
  - The TrueNAS catalog Tailscale app has a `host_network` option ([questions.yaml](https://github.com/truenas/apps/blob/master/ix-dev/community/tailscale/questions.yaml)). With it on, the host's SSH is reachable on its tailnet address **(inference)**.
- **Steps:**
  1. Build and push images tagged `$(Build.BuildId)`.
  2. Render the Compose file (see §3).
  3. `ssh truenas 'midclt call -j app.image.pull …'` (24.10 only).
  4. `ssh truenas 'midclt call -j app.update kleingeld -'`, with the YAML in the payload. Stdin payload (`-`) only exists in the 26 client ([api_client README](https://github.com/truenas/api_client)), so on 24.10/25.x pass it as an argument or `scp` a JSON file first.
- **Pros:** TrueNAS stays the owner of the app (UI, logs, upgrades). The job fails if Compose fails. `midclt` always matches the installed TrueNAS version, so the route rides through the REST removal in 26 **(inference)**. Costs fit the free tiers.
- **Cons:** you need an SSH user that can run `midclt` (an admin with sudo), and the deploy key is root-equivalent. Tailscale reaches into the home LAN, so restrict the tag with ACLs.

### Route B: the TrueNAS API called remotely (JSON-RPC over WebSocket)

- **API history:**
  - 24.10 only speaks the legacy WebSocket protocol.
  - 25.04 introduced the versioned JSON-RPC 2.0 API at `/api/current` ([api_client README](https://github.com/truenas/api_client); [25.04 notes](https://www.truenas.com/docs/scale/25.04/gettingstarted/scalereleasenotes/)).
  - REST was deprecated in 25.04 and **"is removed in TrueNAS 26… REST API endpoints do not function in 26 and later"** ([26 version notes](https://www.truenas.com/docs/scale/26/gettingstarted/versionnotes/)). The 25.10 notes add a daily alert when REST is used ([25.10 notes](https://www.truenas.com/docs/scale/25.10/gettingstarted/versionnotes/)).
- **Auth churn:**
  - In 25.04, legacy API keys migrate to the admin user, but keys *created via the API with an allow-list* "are revoked upon upgrade". Keys sent over plain HTTP are automatically revoked ([25.04 notes](https://www.truenas.com/docs/scale/25.04/gettingstarted/scalereleasenotes/)).
  - In 26, `auth.login_with_api_key` is deprecated and planned for removal in 27. Use `auth.login_ex` with `API_KEY_PLAIN` or SCRAM ([26 notes](https://www.truenas.com/docs/scale/26/gettingstarted/versionnotes/)).
- The method docs exist for `app.update`, `app.redeploy`, `app.pull_images` and `app.registry.create` in the [v25.04](https://api.truenas.com/v25.04/api_methods_app.update.html) and [v25.10](https://api.truenas.com/v25.10/api_methods_app.pull_images.html) API references.
- **Verdict:** this route is functionally the same as Route A, but without SSH. In return it needs an HTTPS-reachable UI with a certificate the agent trusts (the Python client only gained `verify_ssl` in 25.10, per the [api_client README](https://github.com/truenas/api_client)), plus an API key that changes form across 24.10, 25.04 and 26/27. Worth considering after upgrading to 25.10 or later. Never use REST.

### Route C: a self-hosted Azure Pipelines agent on TrueNAS

- **Cost and networking:** the agent runs in a container from Microsoft's Dockerfile, configured with `AZP_URL`, `AZP_TOKEN` and `AZP_POOL`. Its PAT or service principal needs the *Agent Pools (read, manage)* scope ([Learn: agent in Docker](https://learn.microsoft.com/en-us/azure/devops/pipelines/agents/docker)). One self-hosted parallel job is free for private projects ([Learn: parallel jobs](https://learn.microsoft.com/en-us/azure/devops/pipelines/licensing/concurrent-jobs)). The agent only connects outward, so no Tailscale is needed.
- **Running Docker from the agent:** you have to mount `/var/run/docker.sock`. Microsoft warns: "serious security implications… can now run as root on your Docker host" ([Learn](https://learn.microsoft.com/en-us/azure/devops/pipelines/agents/docker)). Calling `docker compose` directly would also bypass TrueNAS's app records (`ix-` projects) **(inference)**. The agent would still need to reach `midclt` or the API on the host (back to Route A or B, but over the LAN).
- **Don't install the agent on the TrueNAS host OS.** "Updating TrueNAS using apt or any method other than the TrueNAS web interface can make the system inoperable", and manually installed Docker breaks Apps ([25.04 upgrade notes](https://www.truenas.com/docs/scale/25.04/gettingstarted/scalereleasenotes/)).
- **A VM instead:** VMs have changed a lot. 25.04.0–.1 moved VMs to "Instances", 25.04.2 brought classic VMs back, and Instances-era legacy VMs "are removed in TrueNAS 26" with no migration tool ([26 notes](https://www.truenas.com/docs/scale/26/gettingstarted/versionnotes/)).
- **Verdict:** this removes Tailscale from CI, but adds a root-equivalent agent image to keep patched on the NAS. The build would still run on Microsoft-hosted agents. Second choice.

### Route D: Watchtower-style auto-pull

- **Watchtower is unmaintained:** `containrrr/watchtower` was **archived on 2025-12-17**, and its last release was v1.7.1 (2023-11-11) ([repo](https://github.com/containrrr/watchtower); [announcement](https://github.com/containrrr/watchtower/discussions/2135)). Its README says not to use it in commercial or production environments.
- **It may not work on TrueNAS 26:** it defaults to Docker API **1.25** ([flags.go @v1.7.1](https://github.com/containrrr/watchtower/blob/v1.7.1/internal/flags/flags.go)). Docker Engine 29 "requires API version v1.44 or later" ([Docker 29 release notes](https://docs.docker.com/engine/release-notes/29/)), and TrueNAS 26 ships Docker 29.0.4 ([26 notes](https://www.truenas.com/docs/scale/26/gettingstarted/versionnotes/)). Default Watchtower would fail there **(inference)**.
- **It doesn't fit the app model:** it recreates containers one by one, outside Compose and outside TrueNAS's app records. That means no `depends_on` ordering, so migrations wouldn't run first **(inference)**.
- **Verdict:** rejected.

### Route E: manual "pull and redeploy"

- **Moving tag:** the pipeline pushes `:prod`, then the Admin clicks *Update* in TrueNAS. That runs `app.pull_images` with redeploy (see above). It needs a registry login, so 25.04 or later. On 24.10 the anonymous update check can't see private images **(inference)**.
- **Pinned tags:** the pipeline publishes the rendered Compose file as an artifact, and the Admin pastes it into *Edit* on the custom app.
- **Verdict:** a fine fallback and a fine first step. It's the same TrueNAS operations as Route A, just triggered by hand.

### Route F: `aspire deploy` against the TrueNAS Docker daemon (`DOCKER_HOST=ssh://…`)

- `aspire deploy` runs `docker compose up -d --remove-orphans` itself ([aspire.dev](https://aspire.dev/deployment/docker-compose/)). The project name is `aspire-<env>-<sha8>` ([DockerComposeEnvironmentResource.cs @v13.6.4](https://github.com/microsoft/aspire/blob/v13.6.4/src/Aspire.Hosting.Docker/DockerComposeEnvironmentResource.cs)), and Docker CLI can target a remote daemon over SSH ([Docker docs](https://docs.docker.com/engine/security/protect-access/)).
- **Problem:** the stack would not be a TrueNAS custom app (TrueNAS tracks `ix-` projects), which contradicts the [hosting decision](../../.scratch/music-service/issues/07-hosting-storage-database.md) **(inference)**.
- **Verdict:** rejected.

### Comparison of deploy routes

| Route | TrueNAS still manages the app | Network path from CI | Works on 24.10 with private images | Upgrade exposure | Extra cost | Effort |
|---|---|---|---|---|---|---|
| **A. Microsoft-hosted agent → Tailscale → SSH → `midclt`** | Yes | Ephemeral tailnet node, outbound | Yes, with `app.image.pull` + `auth_config` | Low: `midclt` matches the OS; method names stable 24.10→25.10 | $0 (free tiers) | Medium |
| B. Microsoft-hosted agent → Tailscale → JSON-RPC API | Yes | Tailnet, HTTPS to the UI | Yes (legacy WebSocket on 24.10) | **High**: legacy WebSocket → JSON-RPC (25.04), keys revoked/reshaped (25.04), REST gone (26), `login_with_api_key` gone (27) | $0 | Medium |
| C. Self-hosted agent container/VM on TrueNAS | Only if it calls `midclt`/API | Outbound 443 only | Yes | Medium: docker.sock is root; VM churn (25.04, 26) | $0 (1 free self-hosted job) | Medium-high |
| D. Watchtower | **No** | None (polls) | Needs creds on host | **High**: archived; API 1.25 vs Docker 29's 1.44 | $0 | Low, but wrong |
| E. Manual Update / paste YAML | Yes | None | Paste: yes. *Update* button: 25.04+ | Low | $0 | Low per deploy, manual |
| F. `aspire deploy` via `DOCKER_HOST=ssh` | **No** | Tailnet + SSH | Needs `docker login` on host | Medium | $0 | Low, but wrong |

---

## 3. How the Aspire output fits, and how secrets get to the containers

### What `aspire publish` produces (Aspire 13.6.4, released 2026-10-24 ([releases](https://github.com/microsoft/aspire/releases)))

- `aspire publish` writes `docker-compose.yaml` plus a `.env` file with "expected parameters (unfilled)". It doesn't build images ([aspire.dev Docker Compose](https://aspire.dev/deployment/docker-compose/)).
- `--environment <name>` produces a filled `.env.<name>` file.
- `aspire do push` builds and pushes images to a registry declared with `AddContainerRegistry` / `WithContainerRegistry`. That registry API is **experimental (ASPIRECOMPUTE003)** ([aspire.dev](https://aspire.dev/deployment/docker-compose/)).
- From the source ([DockerComposeServiceExtensions.cs @v13.6.4](https://github.com/microsoft/aspire/blob/v13.6.4/src/Aspire.Hosting.Docker/DockerComposeServiceExtensions.cs), [DockerComposeServiceResourceExtensions.cs](https://github.com/microsoft/aspire/blob/v13.6.4/src/Aspire.Hosting.Docker/DockerComposeServiceResourceExtensions.cs)):
  - Each `AddParameter` (secret or not) becomes a `${NAME}` placeholder in the YAML. The name is upper-cased with `-` turned into `_`.
  - Connection strings are assembled inline from those placeholders.
  - Each project image becomes `${<RESOURCE>_IMAGE}`, defaulting to `<resource>:latest`.
- `WaitFor` becomes `depends_on: condition: service_started`, and `WaitForCompletion` becomes `service_completed_successfully`. `service_healthy` is commented out in the generator ([DockerComposeServiceResource.cs](https://github.com/microsoft/aspire/blob/v13.6.4/src/Aspire.Hosting.Docker/DockerComposeServiceResource.cs)).
- An Aspire dashboard container is included by default (`DashboardEnabled = true`) ([DockerComposeEnvironmentResource.cs](https://github.com/microsoft/aspire/blob/v13.6.4/src/Aspire.Hosting.Docker/DockerComposeEnvironmentResource.cs)).
- The 13.6.4 generator doesn't set a `restart:` policy, and Compose's default is `"no"` ([Compose spec](https://docs.docker.com/reference/compose-file/services/)). Add `restart: unless-stopped` to long-running services via `PublishAsDockerComposeService` **(inference; I didn't confirm whether TrueNAS restarts apps itself at boot)**.

### The mismatch with TrueNAS custom apps

- Compose fills `${…}` from the shell, from `--env-file`, or from a `.env` in the project directory, which is the directory of the first Compose file ([Docker interpolation docs](https://docs.docker.com/compose/how-tos/environment-variables/variable-interpolation/)).
- TrueNAS calls `docker compose -f <rendered file> -p ix-<app> …` with no `--env-file` ([compose_utils.py](https://github.com/truenas/middleware/blob/TS-24.10.2.4/src/middlewared/middlewared/plugins/apps/compose_utils.py)), and the rendered directory is managed by TrueNAS. So **Aspire's `.env` isn't picked up** **(inference)**.

### Three ways to get the secrets in

1. **Render in the pipeline (works on 24.10, 25.x and 26).**
   - Keep the secrets in an Azure DevOps secret variable group or Key Vault: Postgres password, Gmail app password, Azure storage connection string or key.
   - Write `.env.Production`, then run `docker compose -f docker-compose.yaml --env-file .env.Production config`. This prints the fully interpolated file to send with `app.update` **(inference; standard Compose CLI behaviour)**.
   - *Trade-off:* the secrets are then stored in plain text in `/mnt/.ix-apps/app_configs/kleingeld/…`, in every ZFS snapshot or replica of the ix-apps dataset (relevant to [backups](../../.scratch/music-service/issues/14-backups.md)), and in `app.config` output for anyone with the `APPS_READ` role ([crud.py](https://github.com/truenas/middleware/blob/TS-24.10.2.4/src/middlewared/middlewared/plugins/apps/crud.py)). They also show in `docker inspect`.
2. **Keep secrets on a TrueNAS dataset and use the `env_file:` service attribute.**
   - A `secrets.env` file sets container variables directly ([Docker docs](https://docs.docker.com/compose/how-tos/environment-variables/set-environment-variables/)). The pipeline never sees the secrets.
   - *Trade-off:* `env_file` doesn't feed `${}` interpolation, so Aspire's inline connection strings, such as `…Password=${POSTGRES_PASSWORD}`, must be replaced by whole values in the file. That means post-processing Aspire's output.
3. **TrueNAS 26 and later: `include:` with its own `env_file`.**
   - 26-BETA.1 added support for the `include:` key in custom apps (NAS-137498) ([26 notes](https://www.truenas.com/docs/scale/26/gettingstarted/versionnotes/)). An include's `env_file` "define[s] default values when interpolating", and it defaults to `.env` next to the included file ([Compose include](https://docs.docker.com/reference/compose-file/include/)).
   - So the custom app can be just `include: [{path: /mnt/<pool>/kleingeld/docker-compose.yaml}]`. The pipeline copies Aspire's unchanged `docker-compose.yaml`, with image tags in a non-secret `.env`, and the secrets live in a root-only `.env` on the dataset **(inference)**.
   - This is the cleanest option, but 26 is still beta: 26-BETA.3 dated 2026-08-20 is the latest listed ([26 notes](https://www.truenas.com/docs/scale/26/gettingstarted/versionnotes/)).

**Suggested now:** option 1 with pinned image tags. Move to option 3 once on TrueNAS 26 stable.

---

## 4. EF Core migrations during a deploy

- **Microsoft's guidance:**
  - "For automated deployment, use a migration bundle." Run it "as a one-shot deployment job after the database is healthy", and don't make each app replica migrate from its entrypoint.
  - EF Core 9 and later lock migrations, and `Migrate()` throws if the model has pending changes. Check for that in CI with `dotnet ef migrations has-pending-model-changes`.
  - Give the deployment its own identity with schema rights ([EF Core: Applying migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying), updated 2026-08).
- **Aspire integration:** `Aspire.Hosting.EntityFrameworkCore` provides `api.AddEFMigrations("api-migrations")`. `PublishAsMigrationBundle(publishContainer: true)` turns the bundle into its own container image (linux-x64 by default).
  - For Docker Compose, set `.PublishAsDockerComposeService((_, s) => s.Restart = "no")`, and give the migration resource both `.WithReference(db)` and `.WaitFor(db)`.
  - `RunDatabaseUpdateOnStart` is local-only.
  - The page doesn't say which Aspire version introduced this ([aspire.dev EF migrations](https://aspire.dev/integrations/databases/efcore/migrations/)).
- **How this plays out on TrueNAS:**
  - With `api.WaitForCompletion(migrations)`, the generated Compose file has `depends_on: migrations: condition: service_completed_successfully` (generator source above). TrueNAS's `compose up -d` therefore starts the API only after the bundle exits 0. A failed migration makes `compose up` fail, which fails `app.update` and so the pipeline step **(inference from Compose semantics plus [compose_utils.py](https://github.com/truenas/middleware/blob/TS-24.10.2.4/src/middlewared/middlewared/plugins/apps/compose_utils.py))**.
  - Because of `--force-recreate`, the bundle runs on every deploy. That's harmless: bundles apply only missing migrations.
- **Gap:** Aspire maps `WaitFor(db)` to `service_started`, not `service_healthy`, so the bundle can start before PostgreSQL accepts connections. Add a `pg_isready` healthcheck to the Postgres service and switch the migration's `depends_on` condition to `service_healthy` via `PublishAsDockerComposeService`, or have the bundle retry **(inference)**.
- **Not recommended:** running `efbundle` from the pipeline against Postgres over the tailnet, because it exposes the database port. Runtime `Migrate()` at API startup is acceptable with EF 9's lock, but Microsoft prefers a separate step.

---

## 5. What breaks across TrueNAS upgrades

| Version | Change | Effect on this deployment |
|---|---|---|
| **24.10 Electric Eel** (installed: 24.10.2.4, 2025-08-07) | Apps moved from Kubernetes (k3s) to Docker. Custom apps can be installed from a Compose YAML ([24.10 notes](https://www.truenas.com/docs/scale/24.10/gettingstarted/scalereleasenotes/)). Automatic k8s→Docker migration on upgrade from 24.04 had a deadline of 2025-06-01 ([25.04 notes](https://www.truenas.com/docs/scale/25.04/gettingstarted/scalereleasenotes/)). | Already on Docker, so nothing to migrate. **24.10 is marked EOL** in the official client README ([api_client](https://github.com/truenas/api_client)). No registry sign-in. |
| **25.04 Fangtooth** (latest 25.04.2.6) | REST deprecated; JSON-RPC 2.0 over WebSocket introduced. User-linked API keys, with allow-list keys created via the API revoked on upgrade, and HTTP key use revoking the key. Per-app host-IP port binding. Docker Registries sign-in. Compose now runs with `--config /etc/docker` ([25.04 notes](https://www.truenas.com/docs/scale/25.04/gettingstarted/scalereleasenotes/); [compose_utils @25.04.0](https://github.com/truenas/middleware/blob/TS-25.04.0/src/middlewared/middlewared/plugins/apps/compose_utils.py)). | Any `docker login` done by hand as root on 24.10 is ignored after upgrade, so use the Docker Registries screen **(inference)**. Recreate API keys. VMs moved to "Instances" in .0–.1, and classic VMs returned in .2. |
| **25.10 Goldeye** (25.10.0 on 2025-10-28; latest 25.10.7 on 2026-09-02, and the client lists 25.10.8) | Registry mirrors. Automatic app migration when changing the apps pool. **25.10.0.1:** fixed custom apps not upgrading alongside image updates (NAS-138534) and false validation errors. 25.10.1: REST usage alerts. 25.10.7: one failing registry no longer blocks all update checks ([25.10 notes](https://www.truenas.com/docs/scale/25.10/gettingstarted/versionnotes/)). | The best current stable target. |
| **26** (beta: BETA.1 2026-04-07, BETA.2 2026-06-17, BETA.3 2026-08-20) | **REST removed.** `auth.login` and `auth.login_with_api_key` deprecated, removal planned in 27. `include:` in custom apps. Custom-app updates from authenticated (and htpasswd) registries fixed. Docker Engine **29.0.4** (minimum API 1.44). Legacy Instances VMs removed ([26 notes](https://www.truenas.com/docs/scale/26/gettingstarted/versionnotes/); [26 deprecations](https://www.truenas.com/docs/scale/26/gettingstarted/deprecations/)). | Breaks REST scripts, old-style API-key logins (in 27), Watchtower defaults, and VM-hosted agents created under Instances. Makes Aspire's `.env` usable through `include:`. |
| **27** (in development) | Middleware commits target 27.0.0-BETA.1, for example compose progress reporting and broken-app handling ([compose_utils history](https://github.com/truenas/middleware/commits/master/src/middlewared/middlewared/plugins/apps/compose_utils.py)). | Watch the `auth.login_with_api_key` removal. |

**Upgrade path:** update to the latest maintenance release of your current major version, then move to the next major version. You can skip majors only with a fresh install plus a config restore ([26 notes](https://www.truenas.com/docs/scale/26/gettingstarted/versionnotes/)). From 24.10.2.4 that means 25.04.2.6, then 25.10.x, then 26. Back up the TrueNAS config, and snapshot `ix-apps`, before each step.

---

## 6. Azure Pipelines costs (read 2026-10-31)

- **Microsoft-hosted free tier** (private projects): one job, 60 minutes per run, **1,800 minutes a month**. You enable it by linking billing to an Azure subscription ([Learn](https://learn.microsoft.com/en-us/azure/devops/pipelines/licensing/concurrent-jobs)).
- **Self-hosted:** one free parallel job with no time limit ([same page](https://learn.microsoft.com/en-us/azure/devops/pipelines/licensing/concurrent-jobs)).
- A build, push and deploy for this app should take a few minutes, well inside the free tier **(estimate)**.

---

## Open risks

1. **No registry login on 24.10.** Private images need a pre-pull with credentials on every deploy, or an upgrade to 25.04 or later. Upgrading to 25.10 first is the simpler path.
2. **Authenticated-registry custom-app updates** were only fixed in 26-BETA.3 (NAS-141149, NAS-141553). It's unclear whether 25.10.x has this bug, so test a private-image `app.update` / `app.pull_images` on the target version before relying on it.
3. **Secrets stored in plain text** in the ix-apps dataset and its snapshots or replicas (option 1). Decide whether off-site backups of that dataset need encryption.
4. **Downtime on every deploy:** `--force-recreate` recreates PostgreSQL as well as the app containers.
5. **Startup race between migrations and Postgres** unless a healthcheck plus `service_healthy` is added.
6. **API churn:** REST is removed in 26, and `auth.login_with_api_key` is due for removal in 27. Keep to `midclt` over SSH, or to JSON-RPC with `auth.login_ex`.
7. **Tailscale inside CI** gives the pipeline a route into the home LAN. Use a tagged ephemeral key and ACLs that only allow port 22 on the NAS.
8. **The Aspire registry/push APIs are experimental (ASPIRECOMPUTE003)** and may change between 13.x releases. Pin the Aspire version in CI.
9. **Azure egress:** ACR pulls and Blob streaming both count against Azure egress. The 100 GB/month free allowance is stated per customer on the bandwidth page; I didn't verify how it's shared across services.

## Claims I couldn't verify

- Whether **24.10 or 25.x** custom apps accept `include:` (it's only documented as added in 26-BETA.1).
- Whether TrueNAS restarts custom-app containers at boot when the Compose file has no `restart:` policy.
- Whether a root `docker login` on 24.10 makes `app.pull_images` work. The source suggests it would, since 24.10 calls plain `docker compose`, but I didn't test it.
- Whether Watchtower works on Docker 29 with `DOCKER_API_VERSION=1.44`.
- Which Aspire version introduced `AddEFMigrations` / `PublishAsMigrationBundle`.
- Docker Hub's Personal pull limit: the pricing page says 100 pulls/hour, the usage docs say 200 per 6 hours.
- TrueNAS 24.10's end-of-life status rests only on the `truenas/api_client` README.
- That GHCR's "currently free" covers private pulls from outside GitHub Actions. The billing page says container storage and bandwidth are free, without that qualifier.
- The ACR Basic price is the list price for West Europe. Other regions and currencies weren't checked, and the ACR pricing page didn't render prices.
