# Audio lives in Azure Blob Storage while the app runs at home

The app (API, web front end, PostgreSQL, conversion job) runs on a home TrueNAS SCALE server, but all audio (originals and converted streams) is stored in Azure Blob Storage, with Azurite standing in during development. Members upload and play audio directly against Blob using short-lived SAS links, so audio never crosses the home internet connection; the home server only carries pages and API calls.

## Considered Options

- **S3-compatible storage on the TrueNAS** (e.g. Garage, SeaweedFS, MinIO): free and fully self-hosted, but every stream would go over the home upload connection, and development couldn't use Azurite. Rejected.

## Consequences

- A Takedown hides a Release at once, but playback links already handed out keep working until they expire (about 1 hour).
- A small monthly Azure bill (storage plus any streaming beyond the free egress allowance), and a dependency on Azure for playback.
- The Azure storage account needs CORS rules for the site's address so browsers can upload and play directly.
