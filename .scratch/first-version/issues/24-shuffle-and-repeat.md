# 24: Shuffle and repeat

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** A Member turns on shuffle and chooses repeat off / all / one.

**Blocked by:** 23 (Queue basics)

**Status:** ready-for-agent

- [ ] Shuffle: the pressed Track first, the rest random; on mid-Queue shuffles only upcoming Tracks; off restores their original order after the current Track; Play next / Add to queue Tracks keep their place
- [ ] Repeat all wraps to the start; repeat one loops only when a Track ends on its own; Next still moves on
- [ ] PlayerService tests for each rule (randomness injectable)
