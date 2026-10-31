# Backups for the database and audio

Type: grilling
Status: open
Blocked by: —

## Question

How are KleinGeld Music's data backed up, and how would we restore it?

- PostgreSQL on TrueNAS: `pg_dump` on a schedule, ZFS snapshots of the dataset, or both; how often, how many kept, and where an off-site copy goes.
- Audio in Azure Blob: whether soft delete, versioning, or a copy elsewhere (e.g. a TrueNAS Cloud Sync task pulling originals home) is needed, given originals exist nowhere else once uploaded.
- The Data Protection keys volume: back it up with the database or not.
- How much loss is acceptable (a day? a week?), and whether we test a restore before going live.
