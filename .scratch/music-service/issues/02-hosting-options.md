# Hosting: Azure vs VPS vs home server

Type: research
Status: resolved
Blocked by: —

## Question

What would it take to host KleinGeld Music (ASP.NET Core API + web front end + database + audio file storage, built with .NET Aspire) on each of: **Azure**, a **VPS** (e.g. Hetzner, DigitalOcean), and a **home server**?

For up to ~50 Members and a few thousand Tracks (assume ~10 MB per Track before any conversion), compare:
- Monthly cost, including storage and outbound bandwidth for streaming.
- Upload bandwidth and reliability needs (especially for a home server: residential upload speed, dynamic DNS, TLS, uptime).
- Maintenance effort: OS patching, backups, TLS certificates.
- How well each fits .NET Aspire deployment (Azure Container Apps via `azd`, Docker Compose publishing, etc.).
- Options for storing audio files: object storage (Azure Blob, S3-compatible) vs local disk.

## Answer

**Front-runner: one Hetzner EU VPS** running the Docker Compose stack .NET Aspire generates (`AddDockerComposeEnvironment` + `aspire publish`/`deploy`), ~€12–30/month, audio on local disk or Hetzner Object Storage (€6.49/month); 20 TB traffic included covers the ~225 GB/month of streaming. Remote deploy and TLS (e.g. Caddy/Traefik + Let's Encrypt, auto-renewal mandatory) are ours to add. **DigitalOcean** is similar at ~$17–29/month. **Azure Container Apps** fits Aspire best and needs least maintenance but costs ~$50–85/month (PostgreSQL Flexible Server, always-on container, egress). **Home server** is near-free but riskiest: 5–17 Mbit/s sustained upload, home power/ISP uptime, dynamic DNS or tunnel (Cloudflare's free terms restrict mostly-audio sites), heaviest maintenance. Assumes 30 GB audio, ~330 kbit/s streams, 50 Members × 1 h/day; a lower streaming bitrate would cut egress and upload needs 2–3×. Prices read 2026-10-31.

Findings, with citations: `docs/research/hosting-options.md` on branch `research/hosting-options` (pushed to origin; never merged).
