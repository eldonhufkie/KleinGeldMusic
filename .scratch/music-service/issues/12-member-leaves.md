# When a Member leaves or is removed

Type: grilling
Status: resolved
Assignee: Eldon Hufkie
Blocked by: —

## Question

What happens when a Member deletes their account, or the Admin removes a Member?

- Their Artists: if they were the only Member managing an Artist, does the Artist and its Releases stay Published, get taken down, or pass to the Admin?
- Their Playlists, Queue state, and any open Drafts or Submitted Releases.
- Whether the Admin can add or remove the Members who manage an Artist, and how a Member invites another Member to manage an Artist.
- Whether a removed person can submit a new Join Request with the same email.
- Can the Admin themselves be removed, or the Admin role handed to another Member?

## Answer

Decided by Eldon Hufkie on 2026-10-31 in a grilling session.

- **Leave and Remove are both permanent deletion**: a Member who **Leaves** (deletes themselves) or is **Removed** (by the Admin) is gone for good. There is no suspended or deactivated state.
- **Releases belong to the Artist**, not to a Member. When one of an Artist's managing Members goes, its Published Releases, Drafts, Submitted Releases and Pending Changes stay with the Artist and the remaining managers carry on.
- **Last manager goes, the Artist goes**: an Artist whose only managing Member Leaves or is Removed is deleted with them, with all its Releases; their Tracks become Unavailable Tracks on Playlists. The confirmation screen (for Leave and for Remove alike) lists those Artists.
- **Managing an Artist**: any managing Member adds another Member directly by picking them from a list (no invitation to accept). Any managing Member can make any manager Stop managing, themselves included. The last managing Member can't Stop managing; they delete the Artist instead. The Admin gets no special control here. No emails for being added or made to Stop managing; it's visible in the app.
- **The Admin** can't Leave and can't be Removed. Handing the Admin role to another Member is out of scope for the first version; if ever needed, it's a manual database change on the server.
- **Coming back**: no blocklist. Someone who Left or was Removed can submit a new Join Request with the same email; the Admin approves or rejects it as usual.
- **Personal data**: Leave and Remove delete the Member's Playlists, login data (Identity rows, passkeys) and their original Join Request. Nothing is kept for the record. The Queue in `localStorage` is cleared on logout and on deletion.
- **Remove needs a reason**, emailed to the removed person, listing any Artists deleted with them. This is **email 10** (after the nine in [Admin review of Submitted Releases](11-admin-review.md)). Email 9 (Release deleted by the Admin) is not sent for Artists deleted this way. Leave sends no email.
- **Confirming Leave**: re-enter the password (or use a passkey), then a confirmation screen listing the Playlists and the solely-managed Artists, with their Releases, that will be deleted.
- **Cut off at once**: a Removed Member's sessions stop immediately; their next request, including an audio stream request, is refused (don't wait for Identity's default 30-minute security-stamp check).
- **Glossary**: added **Leave**, **Remove** and **Stop managing**; **Artist** now says it is deleted with its last managing Member.
- No ADR: all of this is cheap to change before launch.
