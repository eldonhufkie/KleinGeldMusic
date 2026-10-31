# 23: Queue basics

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** Pressing play on a Track replaces the Queue with the list it came from; Next, Previous, Play next, Add to queue and an up-next panel work.

**Blocked by:** 17 (Play a Track: play link and player bar), 19 (Approve: Published)

**Status:** ready-for-agent

- [ ] Play replaces the whole Queue with the source list starting at that Track (Release order, or the list as shown), leaving out Unavailable Tracks
- [ ] Clicking a Release Track row plays the Release from that Track
- [ ] Previous restarts after 3 s, otherwise goes back one; with repeat off the player stops at the end on the last Track
- [ ] Play next inserts after the current Track; Add to queue appends; available in every Track row menu
- [ ] The up-next panel shows the Queue and can remove Tracks (no reordering)
- [ ] PlayerService tests for each rule
