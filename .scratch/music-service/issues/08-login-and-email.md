# Choose login and email approach

Type: grilling
Status: resolved
Assignee: Eldon Hufkie
Blocked by: 04, 05, 07

## Question

How will Members log in, and how will KleinGeld Music send email?

Decide, using [Login and identity options](04-identity-options.md), [Email sending options](05-email-sending.md), and the outcome of [Choose hosting, storage and database](07-hosting-storage-database.md):
- Identity approach (ASP.NET Core Identity, Keycloak, or a hosted provider) and how the Join Request → approval → login setup flow works with it.
- How email works **without a custom domain** in the first version (none will be bought until after a working first version, so Resend can't send to Members yet). Candidates: Gmail SMTP with an app password; the Admin copying set-password and reset links to Members manually; keep a provider-neutral email seam so Resend can be swapped in once a domain exists.

## Answer

**Identity**: ASP.NET Core Identity as in [Login and identity options](04-identity-options.md): tables in PostgreSQL, the Blazor Web App template's Identity pages (static SSR) with Register removed, a same-origin login cookie (also checked by the audio stream).
- Login is email + password. The template's passkey pages stay as an optional extra. 2FA is offered, required for no one (Admin included).
- Approving a Join Request creates a passwordless Member with the email **marked confirmed** and emails a set-your-password link. The approved email is the login email; Members can change it on the Manage pages.
- Data Protection keys live on a persistent volume, so restarts don't log everyone out or break links.

**Links**: **all links (setup, password reset, change-email) are valid for 15 minutes**: Identity's single default token lifetime, no separate provider. An expired link's page points to Forgot password (works because the email is confirmed on approval) and says the Admin can resend. The Admin gets a "Resend setup email" action; no copyable links.

**Join Requests**: the public page always answers the same way (no Member enumeration). Requests from an existing Member's email, or duplicates of an open request, reach the Admin flagged and can be rejected in one click.

**Email**: production sends through **Gmail SMTP** from a dedicated KleinGeld Gmail account (app password, 2-Step Verification), through the provider-neutral SMTP seam from [Email sending options](05-email-sending.md); MailPit locally via Aspire. Once a domain is bought, Resend replaces it by changing config only. Rejected: no email (Admin hand-delivers links: lets the Admin set others' passwords); single-sender providers sending as `@gmail.com` (fails DMARC alignment, goes to spam).

Emails in the first version:
1. Join Request approved, with the setup link.
2. Join Request rejected: short and polite, no reason given.
3. Password reset.
4. Confirm new email (after a change on the Manage pages).
5. To the Admin: a new Join Request has arrived.

Release review outcome emails stay with the "Admin review experience" fog on the map.

**Language**: the UI and emails are English only for now.

No ADR: every choice here is easy to reverse.
