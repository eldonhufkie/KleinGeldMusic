# Hosting: Azure vs VPS vs home server

Research for ticket `02-hosting-options`. Prices read on **2026-10-31**. All prices exclude VAT. Prices change, so re-check the linked pages before you commit to one.

## Answer

A **single EU VPS (Hetzner) running the Aspire-generated Docker Compose stack** is the cheapest fit with the least effort that is still reasonable: about **€12–30/month**. Audio sits on local disk, and outbound traffic is effectively free (20 TB included). **Azure Container Apps** fits Aspire most smoothly (`aspire deploy` provisions everything), but it costs about **$50–85/month** at this scale. The managed Postgres, the always-on replica and per-GB egress make up most of that. A **home server** costs almost nothing to host, but it carries the most risk. Reliability depends on residential upload and power. Cloudflare's free CDN terms restrict serving mostly audio. And you own every part of the operations work.

| | Azure (Container Apps) | VPS (Hetzner EU) | VPS (DigitalOcean) | Home server |
|---|---|---|---|---|
| Rough monthly cost | **~$50–85** | **~€12–30** | **~$17–29** | **~€0–5** (power) + domain |
| Egress for ~225 GB/mo | ~$11 (100 GB free, then $0.087/GB) | €0 (20 TB included) | $0 (1–4 TB included) | €0, but limited by upload speed |
| Audio storage | Blob Hot LRS, ~$0.60/mo for 30 GB | Local NVMe, or Object Storage €6.49/mo | Local SSD, or Spaces $5/mo | Local disk |
| Maintenance | Lowest: managed OS, TLS and DB backups | OS patching, backups and TLS (Caddy/Traefik automate TLS) | Same as Hetzner | Highest: also hardware, power, dynamic DNS and router |
| Aspire fit | First-class: `aspire deploy` or `azd` | Good: Compose publisher (`aspire publish`/`deploy`); you add a reverse proxy and TLS | Same as Hetzner | Same as VPS; ingress is harder |
| Reliability | High (SLA-backed managed services) | Good (single VM) | Good (single VM) | Depends on your ISP and power |

## Assumptions behind the estimates

- **Catalogue:** 3,000 Tracks × 10 MB = **30 GB** of audio, plus cover art and a small database (well under 1 GB).
- **Bitrate:** a 10 MB Track of about 4 minutes is about **330 kbit/s**, which is about **150 MB per listening hour**.
- **Listening:** 50 Members × 1 hour/day × 30 days = 1,500 hours, which is about **225 GB of outbound traffic per month**.
- **Peak concurrency:** if 15 Members stream at once, that is about **5 Mbit/s** of sustained upload. All 50 at once would be about **17 Mbit/s**. Seeking and buffering add bursts above these figures.
- **App shape:** one container for the ASP.NET Core API, which also serves the web front end, plus PostgreSQL. The real Aspire AppHost may split the front end into its own container. On a VPS that changes nothing. On Azure it adds a little compute.
- **Region:** Azure West Europe, Hetzner EU-Central, DigitalOcean list prices.

## Azure

### Cost

Unit prices come from the first-party [Azure Retail Prices API](https://prices.azure.com/api/retail/prices) for `westeurope` (the HTML pricing pages render prices client-side). Read on 2026-10-31.

| Item | Unit price | Estimate |
|---|---|---|
| Container Apps, Consumption: 1 replica, 0.5 vCPU / 1 GiB, min replicas = 1 | vCPU active $0.000034/s, idle $0.000004/s; memory $0.000004/GiB·s ([API, ACA](https://prices.azure.com/api/retail/prices?$filter=serviceName%20eq%20%27Azure%20Container%20Apps%27%20and%20armRegionName%20eq%20%27westeurope%27)). Free grant each month: 180,000 vCPU-s, 360,000 GiB-s and 2M requests ([pricing page](https://azure.microsoft.com/en-us/pricing/details/container-apps/)) | ~$13 if always idle, ~$47 if always active; **~$26** at about one-third active |
| Blob Storage, Hot LRS (audio) | $0.0196/GB·month; reads $0.0043 per 10K ([API, Storage](https://prices.azure.com/api/retail/prices?$filter=serviceName%20eq%20%27Storage%27%20and%20armRegionName%20eq%20%27westeurope%27%20and%20skuName%20eq%20%27Hot%20LRS%27%20and%20productName%20eq%20%27General%20Block%20Blob%20v2%27)) | 30 GB, about **$0.60**, plus a few cents of reads |
| Internet egress (Zone 1) | First 100 GB/month free, then $0.087/GB ([bandwidth pricing](https://azure.microsoft.com/en-us/pricing/details/bandwidth/)) | 125 GB × $0.087, about **$11** |
| PostgreSQL Flexible Server, Burstable B1ms | $0.0199/hour ([API, B1MS](https://prices.azure.com/api/retail/prices?$filter=serviceName%20eq%20%27Azure%20Database%20for%20PostgreSQL%27%20and%20armRegionName%20eq%20%27westeurope%27%20and%20contains(meterName,%27B1MS%27))); storage $0.1369/GB·month ([API, PG storage](https://prices.azure.com/api/retail/prices?$filter=serviceName%20eq%20%27Azure%20Database%20for%20PostgreSQL%27%20and%20armRegionName%20eq%20%27westeurope%27%20and%20contains(productName,%27Storage%27))) | $14.50 + 32 GB × $0.137, about **$19** |
| Container Registry, Basic (Aspire provisions one) | $0.1666/day ([API, ACR](https://prices.azure.com/api/retail/prices?$filter=serviceName%20eq%20%27Container%20Registry%27%20and%20armRegionName%20eq%20%27westeurope%27)) | about **$5** |
| Log Analytics or monitoring | not priced here | a few dollars at low log volume (unverified) |
| **Total** | | **~$60/month** (range ~$50–85, depending mostly on how many hours the replica is active) |

What drives the cost:
- **Replica.** With min replicas = 0, compute is close to free, because no usage is billed when an app has no replicas ([pricing page](https://azure.microsoft.com/en-us/pricing/details/container-apps/)). The trade-off is cold starts on the first request.
- **Database.** Running Postgres as a container inside the environment instead of Flexible Server saves about $19, but you lose managed backups and durable storage.
- **Egress.** Egress grows linearly with listening. At 10× the usage (2.25 TB) it would be about $190/month, while a Hetzner VPS would still be €0.

### Aspire fit

Best of the three. The Azure Container Apps deployment flow:
- Run `aspire add azure-appcontainers` and declare `AddAzureContainerAppEnvironment` in the AppHost.
- Then run `aspire deploy` ([aspire.dev: Container Apps](https://aspire.dev/deployment/azure/container-apps/)). It provisions "the Container Apps environment, Azure Container Registry, managed identity" and pushes images.
- An existing `azd` workflow is still supported ([same page](https://aspire.dev/deployment/azure/container-apps/)).
- App Service and AKS are also full publish and deploy targets ([aspire.dev: deploy with Aspire](https://aspire.dev/deployment/deploy-with-aspire/)).

The old learn.microsoft.com Aspire deployment overview now redirects (301) to aspire.dev.

### Maintenance

No OS patching. Container Apps provides TLS on its ingress, and Flexible Server handles database backups. (These are standard platform features; this note did not cite them line by line.) What remains for you: image updates, cost monitoring, and the Blob lifecycle and backup policy.

### Audio storage

Use Blob Storage. Serve audio either through the API, which streams from Blob, or with short-lived SAS URLs so clients read Blob directly. SAS URLs offload streaming from the container, so the replica stays idle more often. Either way, the bytes count as Azure internet egress.

## VPS

### Hetzner Cloud (EU-Central)

| Plan | Spec | Price/month | Traffic |
|---|---|---|---|
| CX23 (Cost-Optimized) | 2 vCPU, 4 GB, 40 GB NVMe | €5.99 | 20 TB |
| CX33 (Cost-Optimized) | 4 vCPU, 8 GB, 80 GB NVMe | €8.99 | 20 TB |
| CPX12 (Regular) | 1 vCPU, 2 GB, 40 GB NVMe | €11.99 | 20 TB |
| CPX22 (Regular) | 2 vCPU, 4 GB, 80 GB NVMe | €19.99 | 20 TB |

Sources: [Cost-Optimized](https://www.hetzner.com/cloud/cost-optimized) and [Regular Performance](https://www.hetzner.com/cloud/regular-performance), read 2026-10-31.

- All Cost-Optimized plans were marked **"currently unavailable"** on 2026-10-31.
- Overage traffic is €1.00 per extra TB in the EU. US locations include only 1–5 TB and cost more: CPX11 is €17.99 ([Regular Performance](https://www.hetzner.com/cloud/regular-performance)).
- Backups: 7 rotating slots per server ([Hetzner docs](https://docs.hetzner.com/cloud/servers/backups-snapshots/overview/)), priced relative to the instance, starting at €1.198/month. Snapshots are €0.0143/GB·month ([Regular Performance](https://www.hetzner.com/cloud/regular-performance)).
- Object Storage: €6.49/month includes 1 TB stored and 1 TB egress. Further egress is €1.00/TB, and ingress and S3 API calls are free ([Object Storage](https://www.hetzner.com/storage/object-storage)).

**Estimate:**
- CPX22 + backups: about €20 + €4 = **€24/month**.
- Add Object Storage for off-site backups or audio: about **€30**.
- If CX33 becomes available: about **€11–17**.
- 225 GB of egress is about 1% of the included 20 TB, so it costs nothing.

### DigitalOcean

- Basic Droplets: 1 GiB / 25 GiB / 1,000 GiB transfer for $6; 2 GiB / 50 GiB / 2,000 GiB for $12; 4 GiB / 80 GiB / 4,000 GiB for $24 ([Droplet pricing](https://www.digitalocean.com/pricing/droplets)).
- Transfer overage is $0.01/GiB, pooled across the team's Droplets ([bandwidth billing docs](https://docs.digitalocean.com/platform/billing/bandwidth/)).
- Spaces: $5/month includes 250 GiB and 1 TiB of outbound transfer ([Spaces pricing](https://www.digitalocean.com/pricing/spaces-object-storage)).

**Estimate:** a 2 GiB Droplet with Spaces is about **$17**. A 4 GiB Droplet with local disk is about **$24**, plus the backups add-on (not priced here). Egress fits inside the allowance.

### Aspire fit (both VPS options)

Good, with gaps you fill yourself:
- `builder.AddDockerComposeEnvironment("env")` makes all resources publish as Compose services.
- `aspire publish` writes `docker-compose.yaml` and `.env` to `aspire-output`.
- `aspire deploy` runs `docker compose up -d --remove-orphans` ([aspire.dev: Docker Compose](https://aspire.dev/deployment/docker-compose/)).

The docs **do not cover** remote hosts over SSH, TLS, or a reverse proxy ([same page](https://aspire.dev/deployment/docker-compose/)). Plan on one of these:
- Run publish in CI, then `scp` the output and run `docker compose up` on the VPS.
- Point Docker at the VPS with `DOCKER_HOST=ssh://…`.

In both cases, add a Caddy or Traefik container for HTTPS.

### Maintenance

- **OS:** patch it yourself. Unattended security upgrades cover most of it.
- **Docker images:** keep them updated.
- **Backups:** Postgres dumps plus the audio volume, ideally copied off-box (Object Storage or Spaces), in addition to provider snapshots.
- **TLS:** Let's Encrypt certificates are 90 days by default, and Let's Encrypt recommends renewing every 60 days ([LE FAQ](https://letsencrypt.org/docs/faq/)). Lifetimes shrink to 64 days on 2027-02-10 and to 45 days on 2028-02-16 ([LE: from 90 to 45](https://letsencrypt.org/2025/12/02/from-90-to-45/)). Manual renewal is therefore not viable. Use an ACME client that renews on its own (Caddy and Traefik do). ARI-coordinated renewals are exempt from rate limits ([LE rate limits](https://letsencrypt.org/docs/rate-limits/)).

### Audio storage

At 30 GB, local NVMe is fine: it is simple and has no request costs. It does tie the audio to one VM, so back it up off-box. S3-compatible object storage (Hetzner or Spaces) decouples storage from the server for about €5–6.50/month. It also makes a later move to Azure Blob or another S3 store a matter of swapping the adapter.

## Home server

### Cost

No hosting fee. You pay for electricity and a domain. A ~20 W mini-PC running 24/7 uses about 15 kWh/month, which is a few euros (this is an assumption, not a cited figure). Hardware is extra if you don't already own it.

### Upload bandwidth and reliability

- **Upload:** the server needs about **5 Mbit/s** of sustained upload for 15 concurrent streams and about **17 Mbit/s** for all 50, plus headroom for seeks and other household use. Many residential connections are asymmetric, so check your actual upload speed.
- **Uptime:** depends on home power, the ISP, and the router. There is no SLA.
- **Dynamic IP:** you need dynamic DNS, or a tunnel that avoids inbound connections entirely. Cloudflare Tunnel uses "an outbound-only connection model", so no public IP or port forwarding is needed ([Cloudflare Tunnel docs](https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/)).
- **Cloudflare caveat:** Cloudflare's CDN terms for the Free, Pro and Business plans restrict serving "a disproportionate percentage of pictures, audio files, or other large files" without paid services ([Cloudflare service-specific terms](https://www.cloudflare.com/service-specific-terms-application-services/)). A streaming site proxied through Cloudflare's free tier is at risk. The terms do not say whether this applies to traffic through Tunnel, so treat it as a risk to verify.
- **TLS:** same as a VPS (Caddy or Traefik with Let's Encrypt). Wildcard certificates require the DNS-01 challenge ([LE FAQ](https://letsencrypt.org/docs/faq/)), which also works without inbound port 80.

### Maintenance

Highest of the three:
- Everything a VPS needs.
- Hardware failure and disk replacement.
- Power cuts.
- Router and firewall configuration.
- Keeping an off-site backup, since a fire or theft takes out both the server and its local backups.

### Aspire fit

The same Docker Compose path as a VPS. Ingress (dynamic DNS or a tunnel) is the extra work.

## Open points (not verified here)

- Whether the Azure free account offers 12 months of free Flexible Server B1ms usage. That would remove about $15/month in year one.
- The exact Log Analytics cost for an Aspire ACA deployment, and whether `aspire deploy` creates a workspace at all. The [Container Apps page](https://aspire.dev/deployment/azure/container-apps/) does not say.
- DigitalOcean backups add-on pricing, and Hetzner's percentage-based backup price beyond the "starting at €1.198" figure.
- Whether a lower bitrate (for example 128–192 kbit/s Opus or AAC) is used for streaming. That would cut egress and upload requirements by roughly 2–3×. It matters most for Azure egress and home upload.
