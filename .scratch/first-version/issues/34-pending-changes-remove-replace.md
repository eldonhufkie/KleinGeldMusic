# 34: Pending Changes: remove and replace Tracks; fold-in

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** A managing Member removes or replaces Tracks on a Published Release as Pending Changes. The Admin can play old and new audio; Pending Changes can be Withdrawn or Rejected, and fold into the Release on Takedown or Reject.

**Blocked by:** 30 (Takedown and play-link revocation), 33 (Pending Changes: add Tracks)

**Status:** ready-for-agent

- [ ] Replacing a Track's audio keeps the same Track, writes a new blob name, and Members hear the old audio until approval
- [ ] The Admin can play both old and new audio of a replaced Track
- [ ] On approval, removed Tracks and old audio stop being playable for Members (policy rotation)
- [ ] Pending Changes can be Withdrawn to Draft or Rejected with a reason (editable, email 8)
- [ ] A Takedown or Admin Reject of the Release folds the Pending Changes into it for whole review on resubmission; deleting discards them
- [ ] API tests (Aspire testing seam) for each rule
