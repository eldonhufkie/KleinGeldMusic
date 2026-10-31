# Login and identity options

Type: research
Status: resolved
Blocked by: —

## Question

Which way of handling Member logins best fits KleinGeld Music's join flow? The flow: an outsider submits a Join Request (name, email, message) on a public page → the Admin approves → the approved person gets an email link to set up their login. There is no open sign-up.

Compare for .NET 10:
- **ASP.NET Core Identity** (built-in, own database tables, Identity API endpoints).
- **Keycloak** (self-hosted; has a .NET Aspire integration).
- **A hosted provider** (e.g. Microsoft Entra External ID, Auth0) on a free tier.

For each: how the approval-then-invite flow is implemented, how it works with the front-end options under consideration (Blazor WebAssembly and Blazor static SSR), cost at ~50 Members, password reset and optional passkeys/social login, and how much it adds to hosting and maintenance (especially if self-hosted on a VPS or home server).

## Answer

**Use ASP.NET Core Identity.** When the Admin approves a Join Request, create the Member without a password and email a set-your-password link built from a password-reset token. Start from the Blazor Web App template's Identity pages (static SSR; they work under WebAssembly interactivity too, and are where .NET 10 passkeys ship) and remove the Register page. Keycloak is too heavy to self-host for 50 Members (~1.25 GB RAM, Aspire integration in preview). Entra External ID and Auth0 are free at this size but make invite-only awkward.

Findings, with citations: `docs/research/identity-options.md` on branch `research/identity-options` (pushed to origin; never merged).
