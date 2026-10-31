# 17: Play a Track: play link and player bar

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** A managing Member opens their Draft's Release page and plays a Ready Track. The player bar in `MainLayout` keeps playing while they move between pages. This is the player's tracer bullet.

**Blocked by:** 13 (Convert Tracks)

**Status:** ready-for-agent

- [ ] The client asks the API for a play link; the API checks the cookie and the access rule and returns a ~10-minute service SAS read link signed against the container's stored access policy (created if missing)
- [ ] Access rule: Published live Tracks for every Member; anything else only for the Admin and the Artist's managers
- [ ] One `<audio>` element and the player bar live in `MainLayout` outside `@Body`; a scoped `PlayerService` drives a small JS module through a narrow interface
- [ ] Play/pause and seek work; navigation never restarts playback
- [ ] The Release page layout matches what Members will later see (cover, details, numbered Track rows)
- [ ] API tests (Aspire testing seam): managers and the Admin get a link for a Draft Track, other Members are refused; the link plays from Azurite with range requests
- [ ] PlayerService tests for play, pause and seek against the fake audio module
