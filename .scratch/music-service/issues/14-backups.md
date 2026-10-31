# Backups for the database and audio

Type: grilling
Status: resolved
Assignee: Eldon Hufkie
Blocked by: —

## Question

How are KleinGeld Music's data backed up, and how would we restore it?

- PostgreSQL on TrueNAS: `pg_dump` on a schedule, ZFS snapshots of the dataset, or both; how often, how many kept, and where an off-site copy goes.
- Audio in Azure Blob: whether soft delete, versioning, or a copy elsewhere (e.g. a TrueNAS Cloud Sync task pulling originals home) is needed, given originals exist nowhere else once uploaded.
- The Data Protection keys volume: back it up with the database or not.
- How much loss is acceptable (a day? a week?), and whether we test a restore before going live.

## Answer

Decided by Eldon Hufkie on 2026-10-31 in a grilling session.

- **Targets**: lose at most **1 hour** of data; restore **the same evening**, by hand. Protects against our own mistakes (bad migration, app bug, wrong delete), a leaked or misused storage key, and losing the TrueNAS pool or the house (fire, theft). Azure losing data on its own (LRS) is accepted. The pool is a 2-wide mirror.
- **Retention**: every copy (snapshots, dumps, off-site, blob soft delete) is kept **30 days** at most, so a Leave, Remove or Delete is complete within 30 days and a restore can't bring back anything older. The Leave and Remove confirmation (see [When a Member leaves or is removed](12-member-leaves.md)) says: "Backups containing your data are erased within 30 days."
- **Database, local**: TrueNAS periodic ZFS snapshots of the `kleingeld` dataset (PostgreSQL data and Data Protection keys): hourly kept 48 h, daily kept 30 days. Crash-consistent; the fast undo (roll back or clone).
- **Database, off-site**: a TrueNAS cron job runs `docker exec <postgres> pg_dump -Fc` hourly into `kleingeld/backups` (local dumps kept 48 h). An hourly TrueNAS Cloud Sync PUSH, with **remote encryption** on, sends them to a **separate Azure storage account in another region**: Cool tier, a container with a 30-day time-based immutability policy, a lifecycle rule deleting after 30 days, and an access key held only by TrueNAS. No WAL archiving.
- **Audio**: on the audio storage account, **blob soft delete and container soft delete, 30 days**. No blob versioning, so **blob names are write-once**: the app never overwrites a blob, and replacing a Track writes a new blob name (a spec requirement). An hourly Cloud Sync PULL in SYNC mode copies the whole container (originals, converted `.m4a`, covers) into a `kleingeld/audio` dataset, covered by daily snapshots kept 30 days. Check the pool has free space about the size of the container.
- **Keys and secrets**: Data Protection keys stay local only, never off-site; after a disaster everyone logs in again. The owner's password manager holds the Cloud Sync encryption password, the backup storage account key and a copy of `secrets.env`, so a house fire doesn't take the keys to the backups. The `ix-apps` dataset (plain-text secrets) is never copied off-site.
- **Alerts**: TrueNAS email alerts to the owner through the dedicated Gmail account, covering failed snapshot and Cloud Sync tasks. The dump script exits non-zero on failure and its output is not hidden, so TrueNAS emails it (verify during setup). No external heartbeat service.
- **Restore**: a short runbook in the spec. Undelete soft-deleted blobs first, then restore the database. Blobs uploaded after the restored dump are harmless orphans and stay. Rehearse once before launch by restoring a dump and a sample of audio into `kleingeld-test`, with outgoing email pointed at MailPit. Rehearse again only after a major PostgreSQL or TrueNAS upgrade. No automated restore tests.
- **Test environment**: `kleingeld-test` is not backed up.
- **Setup chores for the spec**: the two snapshot tasks, the dump cron job and script, both Cloud Sync tasks, the backup storage account with its immutability and lifecycle policies, soft delete on the audio account, TrueNAS email alerts, and the password manager entries.
- No ADR (nothing here is hard to reverse) and no new glossary terms.
