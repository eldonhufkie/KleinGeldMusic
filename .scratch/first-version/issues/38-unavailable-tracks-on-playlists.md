# 38: Unavailable Tracks on Playlists

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** Tracks whose Release was taken down, rejected or deleted, or that were removed from their Release, stay on Playlists greyed out and come back if the Release is Published again.

**Blocked by:** 30 (Takedown and play-link revocation), 35 (Create a Playlist and add Tracks)

**Status:** ready-for-agent

- [ ] Unavailable Tracks keep their stored title and Artist name, greyed out with an "Unavailable" label and no links, even after deletion
- [ ] They come back playable in the same place when their Release is Published again
- [ ] "Remove unavailable Tracks (n)" with confirmation; single ones can still be removed
- [ ] The player leaves them out when building a Queue from the Playlist
- [ ] API tests (Aspire testing seam) across Takedown, republish, deletion and bulk clear
