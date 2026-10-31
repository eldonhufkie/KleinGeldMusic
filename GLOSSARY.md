# KleinGeld Music

An invite-only music platform where a small group of artists publish their own music and approved members stream it.

## People

**Member**:
A person who has been approved to join and can log in. Every Member can listen.
_Avoid_: User, account, listener

**Admin**:
The Member who approves Join Requests, reviews Releases, and can remove any content.
_Avoid_: Moderator, owner

**Join Request**:
A request from someone outside the group to become a Member, which the Admin approves or rejects.
_Avoid_: Sign-up, registration, invite

**Artist**:
A public name music is published under, managed by one or more Members (a solo act or a band).
_Avoid_: Band, creator, uploader, artist account

## Music

**Track**:
A single piece of recorded music, always part of exactly one Release.
_Avoid_: Song, file, upload

**Release**:
A collection of Tracks by an Artist, with its own cover art and one Release type.
_Avoid_: Album (when meaning any release), record

**Release type**:
What kind of Release it is, chosen by its Artist: **Album**, **EP**, or **Single**. Used for grouping when browsing; never worked out from the number of Tracks.
_Avoid_: Format, category

**Draft** / **Submitted** / **Published** / **Rejected**:
The stages of a Release: being prepared by its Artist; awaiting the Admin's review; visible to all Members; returned by the Admin with a reason (whether it was Submitted or already Published).
_Avoid_: Pending, live, approved, declined

**Withdraw**:
An Artist taking back its Submitted Release before the Admin has reviewed it, returning it to Draft.
_Avoid_: Cancel, retract, unsubmit

**Pending Changes**:
Added, removed or replaced Tracks on a Published Release that wait for the Admin's review while Members keep seeing the Release as it was. A Release has at most one set. Pending Changes are Draft, Submitted or Rejected; once approved they become part of the Release.
_Avoid_: Revision, update, new version

**Takedown**:
An Artist hiding its own Published Release from other Members; it can later be resubmitted. The Admin never does a Takedown; the Admin Rejects or deletes.
_Avoid_: Unpublish, delete

**Unavailable Track**:
A Track on a Playlist that can no longer be played because its Release was taken down or deleted, or the Track was removed from its Release; shown greyed out rather than removed.
_Avoid_: Broken track, missing track

**Single mark**:
A mark on a Track saying it was also put out as a Single, shown as an 'S' badge wherever the Track appears. Independent of the Release type of the Track's own Release.
_Avoid_: Single (when meaning the mark), hit

**Rights Declaration**:
An Artist's statement, made when submitting a Release, that they own the rights to its music.
_Avoid_: Copyright check, licence

**Playlist**:
A Member's personal, ordered list of Tracks drawn from any Releases.
_Avoid_: Queue, mix, collection

**Queue**:
The ordered Tracks the player will play in this browser, built from what the Member last pressed play on plus anything they added; not saved to their account and never shared.
_Avoid_: Up next, now playing list
