# Admin review of Submitted Releases

Type: grilling
Status: resolved
Assignee: Eldon Hufkie
Blocked by: 10

## Question

How does the Admin review Submitted Releases, and what does the Artist hear back?

- How the Admin finds Submitted Releases (a review list, a count, an email?).
- Listening before publishing: the Admin must play Tracks that other Members can't see yet.
- Approve / Reject: is a reason required on Rejected, can the Admin publish only some Tracks, and what does a Rejected Release let the Artist do next.
- Re-review after Tracks change on a Published Release: is the old version still visible while it waits?
- Whether the Artist's Members get emails for review outcomes (adding to the five emails in [Choose login and email approach](08-login-and-email.md)), or only see it in the app.
- What the Admin can remove directly (any Release, Artist, Playlist entry) and how that differs from an Artist's Takedown.

## Answer

Decided by Eldon Hufkie on 2026-10-31 in a grilling session.

- **Finding work**: a Review page lists Submitted Releases and Submitted Pending Changes together, oldest first; its navigation link shows a count badge; the Admin gets an email on each submission.
- **Listening**: Tracks that aren't Published can be streamed only by the Admin and the Members who manage the Release's Artist (same stream endpoint and cookie check, plus that access rule). The Admin reviews on a Release page that looks like the one Members will see, using the normal player and Queue.
- **Approve / Reject**: always the whole Release, never some of its Tracks. Rejected needs a free-text reason, shown to the Artist.
- **Submitted**: locked. The Artist can **Withdraw** it back to Draft.
- **Rejected**: can be edited like a Draft, with the reason shown; resubmitting needs a new Rights Declaration. It stays Rejected until resubmitted.
- **Pending Changes**: adding, removing or replacing Tracks on a Published Release creates **Pending Changes** (at most one set per Release). The Release stays Published and Members see and hear the old version. Pending Changes go through Draft, Submitted (can be Withdrawn) and Rejected (editable, reason shown); approving them makes them part of the Release. Changes to other fields still go live without review (see [Release and Track details at upload](10-release-and-track-details.md)).
- **Reviewing Pending Changes**: the Admin sees only what changed (added, removed, replaced Tracks) and can play both the old and the new audio of a replaced Track.
- **Leaving Published**: if a Release with Pending Changes is taken down by its Artist or rejected by the Admin, the Pending Changes fold into the Release, which is then reviewed whole on resubmission. Deleting throws everything away.
- **Admin removal**: the Admin can Reject a Published Release, with a reason (it becomes Rejected and its Tracks become Unavailable Tracks until it is approved again), or delete any Release or Artist. The Admin never does a Takedown and has no access to Playlists.
- **Deleting**: permanent, audio included (only as recoverable as the backups in [Backups for the database and audio](14-backups.md)). An Admin delete needs a reason, emailed to the Artist's Members; an Artist's own Delete needs neither. Deleting an Artist deletes all its Releases.
- **The Admin's own Artists**: the Admin may approve their own Releases; the Rights Declaration is still required. The Admin gets no email about their own submissions or reviews; other managing Members still do.
- **Emails**: four added to the five in [Choose login and email approach](08-login-and-email.md), making nine:
  6. To the Admin: a Release or Pending Changes was Submitted.
  7. To the Artist's Members: approved.
  8. To the Artist's Members: rejected, with the reason.
  9. To the Artist's Members: deleted by the Admin, with the reason.
- **Glossary**: added **Withdraw** and **Pending Changes**; **Rejected** now also covers a Published Release the Admin rejects; **Takedown** is the Artist's action only.
- No ADR: all of this is cheap to change before launch.
