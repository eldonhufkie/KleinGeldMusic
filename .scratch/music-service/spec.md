# Spec: KleinGeld Music first version

Label: ready-for-agent
Source: [Map: KleinGeld Music first version](map.md) and its resolved tickets 01–17
Tickets: [first-version/issues](../first-version/issues/) (48 tracer-bullet tickets, blockers first)

## Problem Statement

A small group of musicians and friends (up to ~50 people) wants somewhere private to publish their own music and listen to each other's. Public streaming services are open to everyone, take a cut, and give the group no say over who joins or what gets published. Sharing files by hand loses the things that make listening pleasant: browsing by Artist and Release, a player that keeps going while you look around, and Playlists that follow you between devices. The group also wants the person running it (the Admin) to decide who joins and to check each Release before others see it, so nobody publishes music they don't own the rights to.

## Solution

KleinGeld Music is an invite-only, web-only music platform. An outsider asks to join through a Join Request on a public landing page; the Admin approves it and the new Member receives an email to set their password. Any Member can create an Artist (a solo act or a band, managed by one or more Members), build Releases (Album, EP or Single) by uploading audio and cover art, and submit them with a Rights Declaration. The Admin reviews each Submitted Release and Publishes or Rejects it. Members browse Published Releases by Artist and Release type, search, play music in a player that keeps playing across pages and on the lock screen, and keep private Playlists. There are no payments.

It runs on the owner's TrueNAS SCALE server, reached over the internet through Tailscale Funnel on a `ts.net` address, with audio in Azure Blob Storage. New versions are built by GitHub Actions, deployed automatically to a tailnet-only Test copy, and promoted to Production with one approval.

## User Stories

### Joining and logging in

1. As an outsider, I want a public landing page that explains what KleinGeld Music is, so that I know what I'm asking to join.
2. As an outsider, I want to submit a Join Request with my name, email and a message, so that the Admin can decide whether to let me in.
3. As an outsider, I want the same confirmation whatever email I enter, so that nobody can use the form to find out who is already a Member.
4. As the Admin, I want an email when a new Join Request arrives, so that I don't have to keep checking.
5. As the Admin, I want to see open Join Requests with their name, email and message, so that I can decide on each.
6. As the Admin, I want Join Requests from an existing Member's email, or duplicates of an open request, flagged and rejectable in one click, so that noise doesn't slow me down.
7. As the Admin, I want to approve a Join Request, so that the person becomes a Member.
8. As the Admin, I want to reject a Join Request, so that people I don't know or don't want aren't let in.
9. As a rejected person, I want a short, polite email, so that I'm not left waiting.
10. As an approved person, I want an email with a link to set my password, so that I can log in for the first time.
11. As an approved person whose link expired (after 15 minutes), I want the page to point me to Forgot password and tell me the Admin can resend, so that I'm not stuck.
12. As the Admin, I want a "Resend setup email" action, so that I can help someone whose link expired without ever seeing their password.
13. As a Member, I want to log in with my email and password, so that I can listen.
14. As a Member, I want to reset a forgotten password by email, so that I can get back in.
15. As a Member, I want to optionally add a passkey or two-factor authentication, so that my login is safer if I care to.
16. As a Member, I want to change my login email, confirmed by a link sent to the new address, so that my account follows my new email.
17. As a Member, I want to stay logged in across server restarts and deploys, so that updates don't interrupt me.
18. As a person who left or was removed, I want to be able to submit a new Join Request with the same email, so that I can come back if the Admin agrees.

### Artists and their Members

19. As a Member, I want to create an Artist, so that I can publish music under that name.
20. As a Member who manages an Artist, I want to add another Member as a manager by picking them from a list, so that my bandmates can manage our Releases.
21. As a Member who manages an Artist, I want to make any manager (including myself) Stop managing, so that the list stays correct when someone quits the band.
22. As the last Member managing an Artist, I want to be told I can't Stop managing and must delete the Artist instead, so that no Artist is left without a manager.
23. As a Member who manages an Artist, I want to delete the Artist, so that its music is removed when we stop.
24. As a Member who manages Artists, I want a My Artists area with a page per Artist, so that I can see all its Releases and their stages in one place.
25. As a Member who manages an Artist, I want each Release on that page badged Draft, Submitted, Published, Rejected (with reason) or Taken down, plus "Pending Changes" where any exist, so that I know what needs my attention.

### Building and submitting a Release

26. As a managing Member, I want to start a New Release as a Draft, so that I can prepare it at my own pace.
27. As a managing Member, I want to choose a Release type (Album, EP or Single), so that listeners can browse it the way I intended.
28. As a managing Member, I want to give the Release a title, release date (which may be in the past), optional description (up to ~2,000 characters) and optional genre, so that listeners see the right details.
29. As a managing Member, I want to upload a square JPEG or PNG cover of at least 1000×1000 px and at most 20 MB, so that the Release looks good everywhere.
30. As a managing Member, I want a non-square or too-small cover rejected with a clear message, so that I know what to fix.
31. As a managing Member, I want to upload audio in FLAC, WAV, AIFF, ALAC, MP3 or AAC, up to 500 MB per file, so that I can use the files I already have.
32. As a managing Member, I want to see real upload progress for each file, so that I know large uploads are working.
33. As a managing Member, I want each Track to show Uploading, Converting, Ready or Failed, so that I know when it's playable.
34. As a managing Member, I want to remove or replace a Failed Track, so that one bad file doesn't block the Release.
35. As a managing Member, I want the first audio file's tags to pre-fill empty Release title, release date, genre and cover art, so that I type less.
36. As a managing Member, I want each Track's title pre-filled from its tag or else its file name, so that I type less.
37. As a managing Member, I want pre-filling never to overwrite what I've typed, so that my edits are safe.
38. As a managing Member, I want embedded cover art that fails the cover checks skipped with a note, so that I know to upload a proper cover.
39. As a managing Member, I want to set each Track's title, Single mark, featured Artists (free text) and explicit flag, so that the Track list is accurate.
40. As a managing Member, I want to drag Tracks to reorder them in a Draft, so that the Track numbers match my intended order.
41. As a managing Member, I want a Release limited to 1–50 Tracks with a clear message, so that I know the rules.
42. As a managing Member, I want Submit disabled until every Track has converted and the cover is set, so that I can't submit something broken.
43. As a managing Member, I want to tick a Rights Declaration at each Submit, so that I confirm I own the rights to the music.
44. As a managing Member, I want to Withdraw a Submitted Release back to Draft, so that I can fix something before the Admin reviews it.
45. As a managing Member, I want a Submitted Release locked, so that the Admin reviews exactly what I submitted.
46. As a managing Member, I want an email when my Release is approved, so that I know it's live.
47. As a managing Member, I want an email with the reason when my Release is rejected, so that I know what to change.
48. As a managing Member, I want to edit a Rejected Release like a Draft, with the reason shown, and resubmit it with a new Rights Declaration, so that I can get it published.
49. As a managing Member, I want to listen to my own unpublished Tracks with the normal player, so that I can check them before submitting.
50. As a managing Member, I want Drafts never to expire, so that I can leave a Release half-done.
51. As one of several managers, I want any of us to be able to create, edit, submit and Take Down the Artist's Releases equally, so that the band shares the work.

### Changing a Published Release

52. As a managing Member, I want to edit the title, cover art, description, release date, genre, Release type and Track details of a Published Release without review, so that small fixes go live at once.
53. As a managing Member, I want adding, removing or replacing Tracks on a Published Release to create Pending Changes, so that the Admin checks new audio before Members hear it.
54. As a managing Member, I want Members to keep seeing and hearing the old version while Pending Changes wait, so that the Release never disappears during review.
55. As a managing Member, I want Pending Changes to go through Draft, Submitted (with Withdraw) and Rejected (editable, with reason) like a Release, so that the flow feels familiar.
56. As a managing Member, I want replacing a Track's audio to keep the same Track, so that it stays on listeners' Playlists.
57. As a managing Member, I want to Take Down a Published Release, so that I can hide it from other Members and resubmit it later.
58. As a managing Member, I want any Pending Changes folded into the Release when I Take it Down, so that it's reviewed whole on resubmission.
59. As a managing Member, I want to Delete a Release permanently, so that it's gone for good when I want that.

### Admin review

60. As the Admin, I want a Review page listing Submitted Releases and Submitted Pending Changes together, oldest first, so that I work through them fairly.
61. As the Admin, I want a count badge on the Admin navigation link, so that I see at a glance when there's review work.
62. As the Admin, I want an email on each submission (except my own), so that I don't miss any.
63. As the Admin, I want to review a Release on a page that looks like what Members will see, and play its Tracks with the normal player, so that I judge what they'll get.
64. As the Admin, I want to approve the whole Release, so that it becomes Published.
65. As the Admin, I want to reject the whole Release with a required reason, so that the Artist knows what to change.
66. As the Admin, I want to see only what changed in Pending Changes (added, removed, replaced Tracks) and play both old and new audio of a replaced Track, so that re-review is quick.
67. As the Admin, I want to Reject an already-Published Release with a reason, so that I can pull music that shouldn't be there.
68. As the Admin, I want to delete any Release or Artist with a reason emailed to its managing Members, so that I can remove content for good.
69. As the Admin who also makes music, I want to approve my own Releases (still with a Rights Declaration) and get no emails about my own submissions, so that I'm not slowed down or spammed.

### Browsing and search

70. As a Member, I want a sidebar with Home, Artists, Releases, Search, Playlists (my own listed beneath), My Artists (when I manage one) and Admin (when I'm the Admin), so that I can move around easily.
71. As a Member on a phone, I want a bottom tab bar (Home, Search, Releases, Playlists, More) with the mini player above it, so that the app works one-handed.
72. As a Member, I want a profile menu holding account settings and Leave, so that account tasks are out of the way.
73. As a Member, I want the home page to show the 12 most recently Published Releases and my Playlists, so that I see what's new.
74. As a Member, I want an A–Z grid of Artists with at least one Published Release, each with its newest Release's cover, so that I can browse by Artist.
75. As a Member, I want a Releases grid filterable by All, Albums, EPs and Singles and sortable by Newest or A–Z, so that I can browse by type.
76. As a Member, I want an Artist page with Albums, EPs and Singles sections (newest release date first, empty sections hidden) and a Play button for the newest Release, so that I can explore an Artist.
77. As a Member, I want a Release page with cover, title, Artist, Release type, release date, genre, total duration, description and a numbered Track list, so that I see everything about it.
78. As a Member, I want each Track row to show title, featured Artists, 'S' and 'E' badges, duration and a menu (Play next, Add to queue, Add to Playlist), so that I can act on any Track.
79. As a Member, I want 'S' and 'E' badges on every Track row in the app but never on Release cards, so that the marks are consistent.
80. As a Member, I want lists to load 48 items at a time with Load more, so that pages stay fast.
81. As a Member, I want one search box with Artists, Releases and Tracks grouped, top 5 each, with "Show all", so that I find things quickly.
82. As a Member, I want search to ignore case and accents and tolerate small typos, and to match prefixes from two characters, so that "kapel" finds "Kapél".
83. As a Member, I want search results to update as I type and the query kept in the URL, so that Back returns to my results.
84. As a Member, I want links to Artists, Releases and Playlists to keep working after a rename, so that links shared in the group don't break.
85. As a Member, I want any unpublished Release's link to show "not found", so that nothing about unpublished work leaks.

### Player and Queue

86. As a Member, I want music to keep playing while I move between pages, so that browsing never interrupts listening.
87. As a Member, I want play/pause, seek, next, previous, volume with mute, shuffle and repeat (off, all, one), so that I control playback.
88. As a Member, I want clicking a Track to replace the Queue with the list it came from (Release, Playlist, Artist page or search results), starting at that Track, so that playback continues naturally.
89. As a Member, I want "Play next" and "Add to queue" on any Track, so that I can line up music.
90. As a Member, I want an up-next panel where I can remove Tracks, so that I can tidy the Queue.
91. As a Member, I want shuffle to start with the Track I pressed and shuffle only what's still to come, and turning it off to restore the original order, so that shuffle behaves predictably.
92. As a Member, I want Previous to restart the Track if it's more than 3 seconds in, otherwise go back one, so that it works like other players.
93. As a Member, I want the player to stop at the end of the Queue with repeat off, and repeat-one to loop only when a Track ends by itself, so that Next always moves on.
94. As a Member, I want Unavailable Tracks left out of the Queue, and a short notice and skip if the playing Track becomes unavailable, so that playback carries on.
95. As a Member, I want my Queue, current Track, position, volume, mute, shuffle and repeat restored (paused) after a reload, so that I pick up where I left off.
96. As a Member, I want the Track's title, Artist and cover on my lock screen and my media keys and headphone buttons to work, so that I can control playback without the browser in front.
97. As a Member, I want a loading screen with progress on first load, so that I know the app is starting.
98. As a Member, I want my Queue cleared when I log out, so that the next person on a shared device doesn't see it.

### Playlists

99. As a Member, I want to create a Playlist with a name (1–100 characters), so that I can collect Tracks.
100. As a Member, I want "New Playlist…" at the top of every Add to Playlist menu, so that I can create and add in one step.
101. As a Member, I want to add a Track to a Playlist from any Track menu (Release page, Playlist page, search results, Queue panel), so that it's always at hand.
102. As a Member, I want to add a whole Release to a Playlist in Track order, so that I don't add Tracks one by one.
103. As a Member, I want new Tracks added at the end, so that my order isn't disturbed.
104. As a Member, I want to be told "Already on <Playlist>" instead of getting a duplicate, and how many Tracks were skipped when adding a Release, so that each Track appears once.
105. As a Member, I want to reorder Tracks with a drag handle that works with mouse and touch, saved at once, so that I can shape the Playlist anywhere.
106. As a Member, I want to remove a Track at once with a few seconds' Undo, so that removing is quick but forgiving.
107. As a Member, I want a Playlist page with name, Track count, total duration, Play and Shuffle, and Track rows linking to Artist and Release, so that I can play and explore it.
108. As a Member, I want a Playlist's cover to be the cover of its first playable Track's Release, so that it looks right without effort.
109. As a Member, I want to rename a Playlist, so that I can fix its name.
110. As a Member, I want to delete a Playlist after confirming, so that I can clear out old ones.
111. As a Member, I want my Playlists ordered by most recently changed, so that the ones I'm working on are at the top.
112. As a Member, I want Unavailable Tracks shown greyed out in place, with their original title and Artist, an "Unavailable" label and no links, so that I know what was there.
113. As a Member, I want an Unavailable Track to play again in the same place if its Release is Published again, so that Takedowns don't wreck my Playlists.
114. As a Member, I want "Remove unavailable Tracks (n)" with confirmation, so that I can tidy up in one go.
115. As a Member, I want clear messages at 100 Playlists or 1,000 Tracks per Playlist, so that I know why I can't add more.
116. As a Member, I want my Playlists private to me (the Admin included can't see them), so that my listening stays mine.

### Leaving and removing

117. As a Member, I want to Leave KleinGeld Music after re-entering my password (or using a passkey), so that nobody else can do it for me.
118. As a Member leaving, I want a confirmation screen listing the Playlists and solely-managed Artists (with their Releases) that will be deleted, so that I know exactly what I'm losing.
119. As a Member leaving, I want to be told backups containing my data are erased within 30 days, so that I know when I'm fully gone.
120. As a Member, I want leaving to delete my Playlists, login data and original Join Request for good, so that nothing personal is kept.
121. As the Admin, I want to Remove a Member with a required reason, seeing the same list of what will be deleted, so that removal is deliberate.
122. As a Removed person, I want an email with the reason and any Artists deleted with me, so that I understand what happened.
123. As the Admin, I want a Removed Member's sessions cut off immediately and their audio stopped within seconds, so that removal takes effect at once.
124. As a remaining manager of a band, I want our Artist and Releases to stay when a bandmate leaves, so that our music isn't lost.
125. As the Admin, I want to be unable to Leave or be Removed, so that the platform always has an Admin.

### Running and maintaining it (owner)

126. As the owner, I want the whole system to start locally with one command via Aspire (with Azurite and MailPit), so that development is easy.
127. As the owner, I want every push and PR to build and run the tests, so that mistakes are caught before release.
128. As the owner, I want a merge to `main` to deploy automatically to a tailnet-only Test copy, so that I can try a release before Members see it.
129. As the owner, I want a "Live" approval to deploy the exact same images to Production, so that what I tested is what goes live.
130. As the owner, I want to roll back by changing an image tag and redeploying, so that a bad release is easy to undo.
131. As the owner, I want database migrations applied once per deploy before the API starts, so that the API never runs against an old schema.
132. As the owner, I want secrets kept only on the server and in my password manager, never in the repository or CI, so that a public repository is safe.
133. As the owner, I want at most 1 hour of data loss and a same-evening restore, so that a mistake or disaster isn't fatal.
134. As the owner, I want email alerts when a backup task fails, so that I find out before I need the backup.
135. As the owner, I want a restore runbook rehearsed once before launch, so that I know it works.

## Implementation Decisions

### Architecture and projects

- .NET 10 throughout. An Aspire AppHost and ServiceDefaults orchestrate: the ASP.NET Core host (API plus the Blazor Web App host and Identity pages), the Blazor WebAssembly client, a conversion worker, a migrator worker, PostgreSQL, and Blob storage (Azurite in development) plus MailPit in development.
- Keep the project count small (lesson from dotnet-podcasts): one API/host, one client, one shared contracts library if useful, the two workers, AppHost, ServiceDefaults, and test projects. No message queue, SignalR, Orleans or second database.
- Front end: Blazor WebAssembly with global interactivity, **no prerendering**; a lightweight loading screen (logo plus a progress bar from Blazor's load-progress CSS variables) shows until WebAssembly is ready. The public landing page with the Join Request form is part of the same app. Identity's account pages stay static SSR from the template, with Register removed.
- The client calls the API on the same origin with the Identity cookie; no bearer tokens (the `<audio>` element can't send them).
- UI and emails are English only.

### Identifiers and URLs

- Every Artist, Release, Track and Playlist has a GUID v7 id created in the app. URLs carry id plus slug (e.g. `/artists/{id}/{slug}`); the id finds the record, a wrong or old slug is corrected by redirect. Fixed from the first migration (ADR 0002).

### Domain model (PostgreSQL via EF Core)

- **Member**: an ASP.NET Core Identity user. Exactly one Member is the Admin (seeded/configured; can't Leave or be Removed; transfer is a manual database change).
- **Join Request**: name, email, message, created time, status (open / approved / rejected), flags for "email belongs to an existing Member" and "duplicate of an open request". Deleted with the Member when they Leave or are Removed.
- **Artist**: name, slug, and a set of managing Members (many-to-many, all equal). An Artist with no managing Member can't exist: removing the last manager is refused; Leave/Remove of the last manager deletes the Artist.
- **Release**: Artist, title, slug, Release type (Album / EP / Single, chosen, never derived), release date (required, display and sort only), description (optional, plain text, ~2,000 chars), genre (optional, one free-text field), cover art (original plus ~300 px and ~1000 px copies), stage, Published date (set on first approval; used for "New Releases" and "Newest" sort), taken-down state, latest rejection reason.
- **Release stages**: Draft → Submitted → Published or Rejected. Submitted can be Withdrawn to Draft. Rejected is editable like a Draft and stays Rejected until resubmitted. A Published Release can be Taken Down by its Artist (hidden, resubmittable, shown as "Taken down" on My Artists) or Rejected by the Admin with a reason. Model "Taken down" so a resubmission goes back through Submitted.
- **Pending Changes**: at most one set per Release, only on a Published Release; holds added, removed and replaced Tracks; has its own stage (Draft / Submitted / Rejected) and rejection reason. Approval merges it into the Release. If the Release is Taken Down or Rejected, the Pending Changes fold into the Release, reviewed whole on resubmission. Deleting a Release discards them.
- **Track**: belongs to exactly one Release; title, position (Track number), Single mark, featured Artists (free text), explicit flag, duration (from ffprobe), conversion status (Uploading / Converting / Ready / Failed), blob names of the original and the converted `.m4a`. Replacing audio keeps the same Track and writes new blobs; the old audio stays live until the Pending Changes are approved.
- **Rights Declaration**: recorded per submission: Release (or Pending Changes), Member, time, wording version. The checkbox wording is fixed text with a version.
- **Playlist**: owner Member, name (1–100 chars, duplicates allowed), last-changed time (bumped by rename, add, remove, reorder, bulk clear), ordered entries. Limits: 100 Playlists per Member, 1,000 entries per Playlist.
- **Playlist entry**: Track reference (nullable, so it survives the Track's deletion), position, and a snapshot of the Track title and Artist name taken when added. Unique per Playlist and Track. An entry is an **Unavailable Track** when its Track is gone, removed from its Release, or its Release isn't Published; it becomes playable again if the Release is Published again.
- Simultaneous edits anywhere: last write wins, no conflict warnings.

### Audio and cover pipeline

- **Storage**: Azure Blob Storage in Production, Azurite in development and tests, same SDK (ADR 0001). Originals, converted `.m4a` files and covers are all kept. **Blob names are write-once**: the app never overwrites a blob; replacing writes a new name.
- **Upload**: the API issues a short-lived SAS write link per file; the browser uploads straight to Blob with real progress, then tells the API the upload is complete. Audio up to 500 MB per file, no length limit.
- **Validation**: by ffprobe, not file extension. Accept FLAC, WAV, AIFF, ALAC, MP3, AAC. Cover art: JPEG or PNG, square, ≥1000×1000 px, ≤20 MB; non-square rejected (no crop tool).
- **Conversion worker**: picks up uploaded Tracks, downloads the original, converts with FFmpeg (via FFMpegCore, or `System.Diagnostics.Process` to avoid CliWrap's extra terms) to AAC ~192 kbps fast-start `.m4a`, uploads it under a new blob name, records duration and status. Failed conversions set Failed. Work state lives in PostgreSQL (no separate queue).
- **Tag pre-fill**: TagLib# reads tags and embedded cover; pre-fill never overwrites typed values; embedded cover goes through the cover checks and is skipped with a note if it fails.
- **Clean-up**: a daily job deletes unattached uploaded blobs older than 24 h. Removing a Track from a Draft deletes its blobs at once. Deleting a Release or Artist deletes its blobs.
- **Streaming**: audio never passes through the home server. The client asks the API for a **play link** for a Track; the API checks the login cookie (security stamp validated on every request, so a Removed Member is refused at once) and the access rule, and returns a SAS read link valid **~10 minutes**. The `<audio>` element plays it straight from Blob with HTTP range requests. The Queue stores Track ids, never links; a link is fetched each time a Track starts and again whenever playback fails (see Player).
- **Access rule**: a Track on a Published Release, in its live version, is playable by every Member. Anything else (a Track on a Draft, Submitted, Rejected or Taken-down Release, a Track added in Pending Changes, or the new audio of a replaced Track awaiting review) is playable only by the Admin and the Artist's managing Members, who use the same play-link endpoint. Old audio replaced by approved Pending Changes is no longer playable by Members.
- **Revoking links**: play links are service SAS links signed against a **stored access policy** on the audio container (so the API signs with the account key; user-delegation SAS can't use stored policies). Whenever something makes a Track unplayable for someone who could play it before (Remove, Leave, Takedown, the Admin Rejecting a Published Release, deleting a Release or Artist, Pending Changes approved that remove or replace Tracks), the API rotates the policy: it creates a new policy, signs new links against it, and deletes the old one. Azure then refuses every outstanding link within about 30 seconds. Other listeners' players refetch a link without noticing more than a brief hiccup; a container holds at most 5 policies, so rotation replaces, never accumulates. Rotation must be serialized (one at a time) so concurrent events can't leave two live policies or none.
- Blob CORS allows the site's `ts.net` origin(s) for browser uploads.
- Covers are served the same way or via cacheable short-lived links; they never sit in a public container.

### API surface (contract shape, not routes)

- **Public (anonymous)**: submit Join Request (always the same response).
- **Admin**: list/approve/reject Join Requests, resend setup email; Review list (Submitted Releases and Pending Changes, oldest first) and pending count; approve/reject (reason required) a Release or Pending Changes; Reject a Published Release; delete a Release or Artist with reason; list Members and Remove with reason (with a preview of what will be deleted).
- **Managing Members**: create/delete Artist; add manager / Stop managing; create/edit Draft Release; request upload links; confirm upload; reorder/remove/replace Tracks; submit (with Rights Declaration) / Withdraw; edit live fields of a Published Release; Pending Changes operations; Take Down; Delete; My Artists views including stages and reasons.
- **Every Member**: home (12 newest Published, own Playlists); Artists A–Z; Releases with type filter and Newest/A–Z sort; Artist page; Release page; search; play link; Playlist CRUD, add Track, add Release (returns skipped count), move, remove, undo-remove (re-add at its position), bulk clear Unavailable; Leave (with password or passkey re-check, and a preview of what will be deleted).
- **Visibility rule**, applied in one place: only Published Releases in their live version (Pending Changes invisible) and Artists with at least one Published Release are visible to Members; anything else answers "not found" unless the caller manages the Artist or is the Admin.
- **Paging**: keyset paging, 48 items per page, everywhere a list can grow.

### Search

- One query over three groups: Artists (name), Releases (title), Tracks (title and featured Artists). Descriptions and genre aren't searched. Only visible (Published) content.
- PostgreSQL `pg_trgm` + `unaccent` with GIN trigram indexes on an unaccented, lower-cased expression. Two characters: prefix match. Three or more: trigram similarity, ranked by score. Each group returns its top 5 with a "Show all" mode using Load more.
- Client debounces ~300 ms and keeps the query in the URL (`/search?q=…`).

### Identity and accounts

- ASP.NET Core Identity on PostgreSQL. Email + password; template passkey pages kept as an optional extra; 2FA offered, required for no one.
- Approving a Join Request creates a passwordless Member with the email marked confirmed and sends a set-password link. All emailed links (setup, reset, change-email) use Identity's single default token lifetime set to **15 minutes**. The expired-link page points to Forgot password and mentions the Admin can resend.
- Data Protection keys on a persistent volume (a bind-mounted host path in Production).
- Security stamp validated on every request (not Identity's default 30-minute interval), so Remove and Leave cut off sessions and stream requests at once.
- **Leave**: password or passkey re-check, then a confirmation listing Playlists and solely-managed Artists with their Releases, and the "Backups containing your data are erased within 30 days" notice. Deletes Playlists, Identity rows and passkeys, and the original Join Request; deletes solely-managed Artists with all their Releases and blobs; removes the Member from other Artists' managers. No email.
- **Remove**: same deletion and confirmation preview, reason required, email 10 sent. Email 9 is not sent for Artists deleted this way.
- The client clears the Queue in `localStorage` on logout and on Leave.

### Email

- A provider-neutral SMTP seam (MailKit). Production: Gmail SMTP from a dedicated KleinGeld Gmail account with an app password; later Resend by config only once a domain exists. Development and Test: MailPit.
- The ten emails:
  1. Join Request approved, with the setup link.
  2. Join Request rejected: short and polite, no reason.
  3. Password reset.
  4. Confirm new email.
  5. To the Admin: a new Join Request arrived.
  6. To the Admin: a Release or Pending Changes was Submitted (not for the Admin's own Artists).
  7. To the Artist's managing Members: approved.
  8. To the Artist's managing Members: rejected, with the reason.
  9. To the Artist's managing Members: deleted by the Admin, with the reason.
  10. To a Removed person: the reason and any Artists deleted with them.
- The Admin gets no emails 7–8 about their own Artists; other managing Members of those Artists still do.

### Player (client)

- One `<audio>` element and the player bar in `MainLayout`, outside `@Body`. A scoped `PlayerService` (one per tab) holds the Queue and playback state and raises a change event; a small JS module drives the audio element and the Media Session API. The `PlayerService` talks to the JS module through a narrow interface (the test seam).
- Queue rules: pressing play replaces the whole Queue with the source list starting at that Track (Release in Track order, Playlist in its order, Artist page or search results as listed), leaving out Unavailable Tracks. "Play next" inserts after the current Track; "Add to queue" appends. The up-next panel can remove, not reorder.
- Shuffle: the pressed Track first, the rest random; turning on mid-Queue shuffles only upcoming Tracks; turning off restores their original order after the current Track; Play next / Add to queue Tracks keep their place.
- Previous restarts if more than 3 s in, else goes back one. Repeat off stops at the end on the last Track. Repeat all wraps. Repeat-one loops only on natural end; Next still moves on.
- **Play links**: the `PlayerService` asks the API for a play link each time a Track starts. If playback fails (most often an expired or revoked link, or a long pause outliving the link), it asks once for a fresh link and resumes at the same position. If the API refuses the link (the Track is no longer playable, or the Member was Removed), the player skips to the next playable Track with a short notice and doesn't try to finish the current one. If the refusal is because the session ended, the player stops and the app returns to login.
- Persisted in `localStorage`: Queue, current Track, position, volume, mute, shuffle, repeat; restored paused, dropping Tracks now unavailable. Storage failures fall back to empty with defaults (full volume, shuffle off, repeat off).
- Media Session: title, Artist, cover; play/pause/next/previous/seek handlers.
- Multiple tabs or devices play independently.

### Navigation and pages

- Sidebar beside the player (Home, Artists, Releases, Search, Playlists with the Member's Playlists beneath, My Artists when managing, Admin with review badge when Admin) and a profile menu (account settings, Leave). Below ~700 px a bottom tab bar (Home, Search, Releases, Playlists, More) with the mini player above it. No Tracks page.
- Pages as in the user stories: Landing (public), Home, Artists, Artist, Releases, Release, Search, Playlists, Playlist, My Artists (per Artist, with stage badges, New Release, managers list), Release editor, Admin (Join Requests, Review, Members).
- Playlist reordering by a drag handle via JS interop (e.g. SortableJS), each move saved at once; Undo notice for a few seconds after remove.

### Hosting and deployment

- Production on TrueNAS SCALE **25.10** as a custom app whose YAML is a fixed stub that `include:`s Aspire's generated `docker-compose.yaml` with `env_file: [secrets.env, release.env]`, all in the `kleingeld` dataset folder. Public through Tailscale Funnel on a `ts.net` address.
- Secrets declared once in the AppHost as secret parameters; development values in AppHost user secrets; Production values in a root-only `secrets.env` written by hand. Nothing secret passes through CI.
- Every compose change (healthchecks, restart policies, bind mounts) is made in the AppHost (`PublishAsDockerComposeService` / `ConfigureComposeFile`); the compose file is generated by `aspire publish` in CI and not committed.
- PostgreSQL data and Data Protection keys are bind-mounted host paths on datasets (so ZFS snapshots cover them).
- Migrator worker applies EF Core migrations and exits; the API waits on it (`WaitForCompletion`, `service_completed_successfully`); the migrator waits for PostgreSQL's `pg_isready` healthcheck (`service_healthy`). `EnableRetryOnFailure` on API and migrator. Restart `unless-stopped` for long-running services, `"no"` for the migrator.
- CI/CD on GitHub Actions in the public GitHub repository. Work on `develop`; every push and PR builds and tests, never deploys. Release = PR from `develop` into `main`. A merge to `main` builds and pushes images tagged with the commit SHA (never `latest`) to public GHCR packages (keep the last 10 versions per image), deploys to **Test** automatically, then waits on a `production` Environment with Eldon as required reviewer before deploying the **same tag** to **Production**.
- Deploy step: an ephemeral `tag:ci` runner joins the tailnet via `tailscale/github-action` (OAuth client), SSHes as a `deploy` user whose key is locked to a forced command, `deploy.sh`, accepting only `deploy test <tag>` or `deploy prod <tag>` plus the generated compose file; it writes the compose file and `release.env` and runs `midclt call app.redeploy <app>`. Rollback: change the tag in `release.env`, redeploy.
- **Test**: custom app `kleingeld-test` with its own dataset folder, PostgreSQL and Blob container, tailnet-only via `tailscale serve` (never Funnel), email to a MailPit container. Starts empty plus a seed step (an Admin, a couple of Members, a few sample Releases); never a copy of Production. Not backed up.

### Backups and restore

- Targets: at most 1 h data loss; same-evening manual restore. Every copy kept 30 days at most.
- Hourly (48 h) and daily (30 days) ZFS snapshots of `kleingeld`.
- Hourly `pg_dump -Fc` via a TrueNAS cron job into `kleingeld/backups` (local dumps kept 48 h); the script exits non-zero on failure with visible output. Hourly Cloud Sync PUSH with remote encryption to a separate Azure storage account in another region: Cool tier, 30-day time-based immutability, 30-day lifecycle delete, key held only by TrueNAS.
- Audio account: blob and container soft delete 30 days, no versioning (hence write-once blob names). Hourly Cloud Sync PULL (SYNC mode) of the whole container into `kleingeld/audio`, covered by daily snapshots kept 30 days.
- Data Protection keys never leave the server; the `ix-apps` dataset is never copied off-site. The password manager holds the Cloud Sync encryption password, backup account key and a copy of `secrets.env`.
- TrueNAS email alerts to the owner via the Gmail account for failed snapshot, cron and Cloud Sync tasks.
- **Restore runbook**: (1) stop the Production app; (2) undelete any soft-deleted blobs needed in the audio account (or restore from `kleingeld/audio` snapshots if the account itself is lost); (3) restore the database: roll back or clone a ZFS snapshot for local mistakes, or download and decrypt the chosen off-site dump and `pg_restore` it into a fresh PostgreSQL; (4) start the app (the migrator brings the schema up to date); (5) spot-check login, browsing and playback. Blobs uploaded after the restored dump are harmless orphans and stay. Rehearse once before launch into `kleingeld-test` with email pointed at MailPit; again only after a major PostgreSQL or TrueNAS upgrade.

### One-off setup chores (owner)

- Upgrade TrueNAS 24.10 → 25.04 → 25.10; then verify a custom app accepts `include:` with an absolute `env_file` path before any workflow work.
- Create the `kleingeld` and `kleingeld-test` dataset folders and paste the custom-app stubs; write each `secrets.env` (mode 600).
- Provision the Azure audio storage account (soft delete on blobs and containers, CORS for the `ts.net` address(es); the app creates and rotates the container's stored access policy itself, so it needs the account key in `secrets.env`) and the separate backup storage account in another region (immutability and lifecycle policies).
- Set up Tailscale Funnel for Production and `tailscale serve` for Test; a Tailscale OAuth client for CI; an ACL letting `tag:ci` reach only TrueNAS port 22; the `deploy` user with its forced-command key and `deploy.sh`.
- Persistent volume (bind mount) for Data Protection keys.
- The dedicated Gmail account with 2-Step Verification and an app password.
- Finish the move to GitHub (`git push --mirror` from Azure DevOps, research branches included); turn on push protection; create the `main` branch and the `production` Environment with Eldon as reviewer.
- The two snapshot tasks, the dump cron job and script, both Cloud Sync tasks, TrueNAS email alerts, password manager entries; then the restore rehearsal.

## Testing Decisions

- **What makes a good test**: it exercises external behaviour through a seam, as a Member, the Admin or an outsider would cause it (an HTTP call, an email arriving, a blob appearing, a Track becoming playable), and asserts on observable results, never on internal classes, EF entities or private methods. Tests should survive a refactor of everything behind the seam.
- **Seam 1: the HTTP API**, the main seam. Tests start the real system through Aspire's distributed application testing (`Aspire.Hosting.Testing`) with real PostgreSQL (so `pg_trgm`/`unaccent` search and keyset paging are tested for real), Azurite, MailPit and FFmpeg, and talk to the API over HTTP with cookie-authenticated clients for different actors (outsider, Member, managing Member, Admin). Emails are asserted by reading MailPit's API (e.g. take the setup link from email 1 and use it). The only substitution is a **controllable clock** behind the system's time abstraction (`TimeProvider`), used for: 15-minute link expiry, the 24 h unattached-blob clean-up, Published-date ordering.
  - Covers: the Join Request → approval → set-password → login flow; non-enumerating responses; Release stage transitions (Draft, Submitted, Withdraw, Published, Rejected, Taken down, Delete) including forbidden transitions; Pending Changes, including the old version staying live and folding on Takedown/Reject; the Rights Declaration being required and recorded; upload limits and validation (non-square cover, too many Tracks, non-audio file); conversion to Ready/Failed with small audio fixtures; the visibility rule ("not found" for unpublished content except managers and Admin); the play-link access rule (including old versus new audio of a replaced Track) and refusal right after Remove; revocation, i.e. a play link issued before a Takedown or Remove stops working against Azurite once the policy rotates, while a freshly issued link for a still-Published Track works (allow for Azure's ~30 s propagation by polling, not sleeping, if Azurite shows any delay); search matching (case, accents, prefix at 2, trigram from 3) and grouping; paging; Playlist rules (no duplicates, skipped count, limits, ordering, Undo, Unavailable Tracks keeping their snapshot and reviving on republish, bulk clear); Leave and Remove deleting the right data and Artists; Artist manager rules (last manager can't Stop managing); which of the ten emails are sent to whom, including the Admin's own-Artist exceptions; write-once blob names on replace.
- **Seam 2: the client `PlayerService`**, as a plain C# class with the JS audio module (and `localStorage`) behind a small interface replaced by a fake in tests. Covers the Queue rules: play replaces the Queue and skips Unavailable Tracks; Play next / Add to queue; shuffle on/off semantics; the 3-second Previous rule; repeat off/all/one; a fresh play link fetched on playback failure and resumed at the same position; skip with notice when the play link is refused; stop on a lost session; persistence and restore (paused, dropping unavailable Tracks, defaults on storage failure); clearing on logout.
- **Not automatically tested in v1**: Blazor page rendering, Identity's template pages, drag-and-drop JS interop, Media Session integration, responsive layout, the deploy workflow and backup tasks (the latter verified by the restore rehearsal and alert checks during setup).
- **Prior art**: none in this repository yet (the solution is empty; the front-end prototype has no tests). Follow Aspire's documented testing template (xUnit with `DistributedApplicationTestingBuilder`) for Seam 1. Tests run in GitHub Actions on every push and PR, so the CI runner needs Docker for the containers and FFmpeg available to the conversion worker.

## Out of Scope

- Payments of any kind (tips, paid downloads, subscriptions).
- Native mobile apps (a later effort could use .NET MAUI over the same API).
- Offline downloads.
- Recommendations, likes, follows, comments, lyrics, listening history, statistics for artists.
- Shared or collaborative Playlists; "Save Queue as Playlist"; adding a whole Artist to a Playlist; Playlist descriptions or uploaded covers.
- Reordering the Queue (drag-to-reorder later); coordinating playback across tabs or devices.
- HLS/DASH streaming; Opus or FLAC streams; WebP covers; a cover crop tool.
- Disc numbers, per-Track credits, ISRC, UPC, label or copyright lines; Compilation as a Release type; filtering by explicit.
- Publishing only some Tracks of a Release; the Admin doing Takedowns or seeing Playlists.
- An Artist photo and bio.
- A licensed commercial catalog.
- Public sign-up without Admin approval; a rejoin blocklist; suspended or deactivated Members.
- Handing the Admin role to another Member (manual database change if ever needed).
- Buying a custom domain, Resend email, and a nicer address than `ts.net` (after a working first version).
- Languages other than English.
- A portfolio showcase site, or hosting the front end on Vercel (it would break the same-origin login cookie).
- Automated restore tests, WAL archiving, blob versioning, an external heartbeat service.

## Further Notes

- **Streaming reconciliation** (decided by Eldon Hufkie on 2026-10-31, after the spec was first drafted): the audio research (ticket 03) defaulted to streaming bytes through the API; the hosting decision (ticket 07, ADR 0001) chose ~1 h SAS links straight from Blob; tickets 09, 11 and 12 assumed 03's instant refusal. Resolved as: the API checks the cookie and access rule and hands out ~10-minute SAS links signed against a stored access policy; the player refetches a link on failure; the policy rotates on any event that takes away access. Audio stays off the home connection, and loss of access takes effect within about 30 seconds instead of ~1 hour. Rejected: proxying audio through the API (doubles the path through the home upload and Tailscale Funnel); ~1 h links with no revocation (a removed person keeps listening for up to an hour, and a long pause can't resume).
- **Scale**: ~50 Members, a few thousand Tracks; a single home server and a single PostgreSQL. Revisit hosting if peak-time slowdowns appear, home outages become unacceptable, or Members grow into the hundreds.
- **Accepted risks**: home power/ISP outages take the service down; Tailscale Funnel is in beta; first load is ~2.2–2.5 MB (brotli) on first visit and after each deploy; conversion time hasn't been benchmarked on the TrueNAS host (expect seconds per Track; measure once deployed).
- **Glossary**: use [GLOSSARY.md](../../GLOSSARY.md) terms in code, UI and emails (Member, not user; Track, not song; Release, not album; Takedown, not unpublish).
- **ADRs**: [0001 Audio in Azure Blob while the app runs at home](../../docs/adr/0001-audio-in-azure-blob-while-app-runs-at-home.md), [0002 GUID v7 ids in URLs](../../docs/adr/0002-guid-v7-ids-in-urls.md).
- **Reference**: the player pattern from the front-end prototype (option A) and dotnet-podcasts; neither is built on directly.
