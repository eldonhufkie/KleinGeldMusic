# 47: Human-only: backups

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** The owner sets up snapshots, database dumps, off-site copies and alerts. Use `/wizard` to walk through it.

**Blocked by:** 45 (Deploy workflow)

**Status:** needs-human

- [ ] ZFS snapshots of `kleingeld`: hourly kept 48 h, daily kept 30 days
- [ ] Hourly cron `pg_dump -Fc` into `kleingeld/backups` (kept 48 h); script exits non-zero on failure with visible output
- [ ] Hourly Cloud Sync PUSH with remote encryption to the backup account
- [ ] Hourly Cloud Sync PULL (SYNC) of the audio container into `kleingeld/audio`, daily snapshots kept 30 days; pool space checked
- [ ] TrueNAS email alerts for failed snapshot, cron and Cloud Sync tasks, verified once
- [ ] Password manager holds the Cloud Sync encryption password and backup account key
