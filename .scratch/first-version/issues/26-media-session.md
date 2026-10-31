# 26: Media Session

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** The playing Track's title, Artist and cover appear on the lock screen, and media keys and headphone buttons work.

**Blocked by:** 17 (Play a Track: play link and player bar)

**Status:** ready-for-agent

- [ ] Media Session metadata updates on every Track change
- [ ] Play/pause/next/previous/seek handlers drive the `PlayerService`
- [ ] PlayerService test: Track changes push metadata to the fake JS module; checked by hand on a phone and desktop
