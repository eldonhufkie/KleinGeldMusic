# 30: Takedown and play-link revocation

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** A managing Member Takes Down a Published Release; it disappears for other Members, playback of it stops within about 30 seconds, and the player moves on. It can be resubmitted.

**Blocked by:** 19 (Approve: Published), 23 (Queue basics)

**Status:** ready-for-agent

- [ ] Takedown hides the Release from other Members ("not found") and badges it Taken down on My Artists
- [ ] The API rotates the stored access policy (new policy, sign against it, delete the old), serialized so only one rotation runs at a time; old links stop working
- [ ] The player fetches a fresh link when playback fails and resumes at the same position; if the link is refused it skips to the next playable Track with a short notice; if the session has ended it stops and returns to login
- [ ] Resubmitting a Taken-down Release goes through Submitted and review again
- [ ] API tests (Aspire testing seam): a link issued before Takedown fails against Azurite afterwards; a fresh link for another Published Track works
- [ ] PlayerService tests for refetch-and-resume, skip on refusal, and stop on lost session
