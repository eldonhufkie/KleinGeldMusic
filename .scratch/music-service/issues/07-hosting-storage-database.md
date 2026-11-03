# Choose hosting, storage and database

Type: grilling
Status: resolved
Blocked by: 02, 03

## Question

Where will KleinGeld Music run, where will audio files live, and which database will it use?

Decide, using [Hosting: Azure vs VPS vs home server](02-hosting-options.md) and [Audio pipeline: formats, conversion, streaming](03-audio-pipeline.md):
- Hosting target (Azure, VPS, or home server).
- Audio storage (object storage vs local disk) and whether originals are kept alongside converted files.
- Database (PostgreSQL, SQL Server, or SQLite).

## Answer

- **Hosting**: the owner's **TrueNAS SCALE** server (ElectricEel-24.10.2.4). Aspire generates a Docker Compose file (`AddDockerComposeEnvironment`), deployed as a TrueNAS **custom app**. API, web front end, PostgreSQL and the conversion job all run there.
- **Public access**: **Tailscale Funnel** on a `*.ts.net` address (automatic HTTPS, no router ports, visitors need no Tailscale). Rejected: Cloudflare Tunnel (needs a domain, which we won't buy before a working first version); Tailscale for every Member (free plan caps at 6 users, and outsiders couldn't reach the Join Request page).
- **Audio storage**: **Azure Blob Storage** in production, **Azurite** in development, same SDK. Browsers upload directly with a short-lived SAS write link (real upload progress). Members play directly from Blob via SAS read links valid ~1 hour, so audio never crosses the home connection. Originals and converted 192 kbps `.m4a` files are both kept; the conversion job on TrueNAS downloads the original, converts, and uploads the result. Rejected: self-hosted S3-compatible storage on TrueNAS (all streaming over home upload, and Azurite wouldn't match production).
- **Database**: **PostgreSQL**, a container in the same Compose stack.
- **Accepted trade-offs**: a Takedown fully applies only once already-issued read links expire (up to ~1 hour); home power/ISP outages take the service down; Funnel is in beta; no custom domain until after the first version, so production email can't use Resend yet (carried into the login and email ticket).
- ADR: [Audio lives in Azure Blob Storage while the app runs at home](../../../docs/adr/0001-audio-in-azure-blob-while-app-runs-at-home.md).


**Amended 2026-10-31** (streaming reconciliation, see the [spec](../spec.md)): play links are ~10-minute SAS links signed against a stored access policy that the API rotates whenever access is taken away, so loss of access applies within about 30 seconds; the player fetches a fresh link when playback fails.
