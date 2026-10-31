# 39: Leave

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** A Member Leaves KleinGeld Music for good after re-checking their password and confirming a list of what will be deleted.

**Blocked by:** 09 (Artist managers: add and Stop managing), 31 (Artist deletes a Release or the Artist), 35 (Create a Playlist and add Tracks)

**Status:** ready-for-agent

- [ ] Leave requires the password (or a passkey) again
- [ ] The confirmation lists their Playlists and solely-managed Artists with their Releases, and says backups containing their data are erased within 30 days
- [ ] Leaving deletes Playlists, Identity rows, passkeys and their original Join Request; deletes solely-managed Artists with all Releases and blobs; removes them from other Artists' managers; revokes play links
- [ ] No email is sent; the client clears the saved player state
- [ ] The Admin can't Leave
- [ ] API tests (Aspire testing seam): everything listed is gone, shared Artists remain with other managers
