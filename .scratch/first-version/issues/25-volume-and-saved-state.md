# 25: Volume and saved player state

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** A Member sets volume and mute, and after a reload finds the Queue, current Track and position restored, paused.

**Blocked by:** 23 (Queue basics)

**Status:** ready-for-agent

- [ ] Volume and mute controls
- [ ] Queue, current Track, position, volume, mute, shuffle and repeat are saved in `localStorage` and restored paused
- [ ] Restoring drops Tracks that have since become unavailable
- [ ] If storage fails, start empty with defaults (full volume, shuffle off, repeat off)
- [ ] Logout clears the saved state
- [ ] PlayerService tests with a fake storage, including a throwing one
