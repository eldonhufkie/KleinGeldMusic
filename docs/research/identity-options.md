# Login and identity options

Research for ticket `04-identity-options` (researched 2026-10-31, .NET 10).

**Question:** Which way of handling Member logins best fits KleinGeld Music's join flow? An outsider submits a Join Request, the Admin approves it, and the approved person gets an email link to set up their login. There is no open sign-up.

## Answer

**Use ASP.NET Core Identity.** For about 50 Members it is the best fit, whichever front end or host is chosen:

- **The join flow is just app code.** On approval, the app creates the Member with no password and emails a set-your-password link built from a standard Identity token. Nothing else has to stay in sync.
- **It adds nothing to run.** Identity lives in the app's own database, so there is no extra container or tenant and no recurring cost.
- **Password reset is built in, and so are passkeys in .NET 10.** Passkeys ship only in the Blazor Web App template, though, and their pages render as static SSR. Both front-end options can use them, as explained below.
- **Keycloak** works but is heavyweight here. Keycloak's own sizing guide puts it at about 1.25 GB of RAM per instance, and it needs its own production database, TLS and upgrades. That cost is real on a VPS or home server.
- **Hosted providers** (Entra External ID, Auth0) are free at this scale. However, neither one is built around "Admin approves, then the person sets up a login": the app has to drive their management APIs, and Entra can turn off self sign-up only through Microsoft Graph.

| | ASP.NET Core Identity | Keycloak (self-hosted) | Entra External ID | Auth0 |
|---|---|---|---|---|
| Approve, then invite | App creates user; `GeneratePasswordResetTokenAsync`; app emails link | App calls Admin REST API: create user, then `execute-actions-email` with `UPDATE_PASSWORD` | App creates user through Microsoft Graph; app emails sign-in link; user sets password with email OTP or reset | App creates user through Management API, then creates a password-change ticket and emails it |
| Turn off open sign-up | Leave out the register page, or block `/register` if `MapIdentityApi` is used | Realm "User registration" OFF (the default) | Graph PATCH `isSignUpAllowed: false` | "Disable Sign Ups" on the database connection |
| Blazor static SSR | Native (template Identity pages are static SSR) | OIDC through `AddKeycloakOpenIdConnect` | OIDC (Microsoft.Identity.Web) | OIDC |
| Blazor WASM (global interactive) | Cookie + `MapIdentityApi`, or server-hosted Identity pages | OIDC/BFF | OIDC/BFF | OIDC/BFF |
| Cost at ~50 Members | Free | Free software; needs RAM and a database | Free (first 50,000 MAU) | Free (up to 25,000 MAU) |
| Password reset | Built in | Built in (toggle) | Built in (email OTP) | Built in |
| Passkeys | Built in from .NET 10 (Blazor Web App template) | Supported from 26.4 | Supported (sign-in and MFA) | "Passwordless" listed on Free |
| Social login | Add `AddGoogle` etc. per provider | Identity brokering | Google, Facebook, Apple, OIDC | Unlimited social connections |
| Extra to host and maintain | None | Container, database, TLS, upgrades, about 1.25 GB RAM | Azure subscription and tenant; no servers | Third-party account; no servers |

## Detail

### 1. ASP.NET Core Identity

**What it is.** Identity keeps users in the app's own database through EF Core. For SPAs and Blazor WebAssembly, `MapIdentityApi<TUser>` adds JSON endpoints: `/register`, `/login`, `/refresh`, `/confirmEmail`, `/resendConfirmationEmail`, `/forgotPassword`, `/resetPassword`, `/manage/2fa` and `/manage/info` ([Identity API endpoints](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0)). Microsoft recommends cookies over tokens for browser apps. The tokens "aren't standard JSON Web Tokens", and the token option "isn't intended to be a full-featured identity service provider" ([same page](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0)).

**Join Request flow.**

1. The public page saves a Join Request (name, email, message) in the app's own table. Identity is not involved yet.
2. The Admin approves it. The API calls `UserManager.CreateAsync(user)` with no password and adds a "Member" role. It then calls `UserManager.GeneratePasswordResetTokenAsync(user)` and emails a link containing that token through an `IEmailSender` implementation. Identity sends its built-in emails, including password reset, through an `IEmailSender` you register ([Identity API endpoints, email section](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0)).
3. The link opens a "set up your login" page. That page posts email, token and new password to `/resetPassword`, which requires exactly those fields ([same page](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0)), or calls `UserManager.ResetPasswordAsync` from a server page. Because the person proved they own the inbox, the app can also mark the email confirmed at this step.
4. **No open sign-up.** The docs list `/register` among the endpoints `MapIdentityApi` always maps and document no option to leave it out ([same page](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0)). If `MapIdentityApi` is used, block `/register` with an endpoint filter or middleware. *(Inference from the documented endpoint list.)* With the Blazor Web App template, simply delete the Register page.

The token approach reuses the reset-password machinery. This is a common pattern, not a dedicated "invite" API; Identity has no invite feature of its own.

**Front-end fit.**

- *Static SSR:* the Blazor Web App template's Identity components "render statically on the server" and "don't support interactivity" ([Blazor auth](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-10.0)), which is exactly the static SSR model.
- *WebAssembly (global interactive):* with Interactive WebAssembly, "the server handles all authentication and authorization requests, and the Identity components render statically on the server" ([Blazor auth](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-10.0)). The state reaches the client through `AddAuthenticationStateSerialization` and `AddAuthenticationStateDeserialization` ([same page](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-10.0)). Alternatively, a standalone WASM app can call `MapIdentityApi` endpoints with cookie auth ([Blazor WASM with Identity](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/webassembly/standalone-with-identity/?view=aspnetcore-10.0)).

**Password reset:** `/forgotPassword` and `/resetPassword` are built in ([Identity API endpoints](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0)).

**Passkeys:** new in .NET 10. They support adding a passkey to an existing account, passwordless account creation and passwordless sign-in. They are "built into the Blazor Web App project template" ([Passkeys](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/?view=aspnetcore-10.0)). The documented limitations are: "Only the Blazor Web App template includes passkey support", "No default attestation validation", and "Passkeys are treated as a primary authentication factor, not as a second factor" ([same page](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/?view=aspnetcore-10.0)). Passkeys are not in the `MapIdentityApi` endpoint list, so a standalone WASM app would need custom endpoints. *(Inference from the endpoint list above.)* Passkeys are bound to the domain where they were registered ([same page](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/?view=aspnetcore-10.0)), so pick the final hostname before Members enroll.

**Social login:** optional, added per provider. This was not researched in depth here because the ticket lists it as optional.

**Cost and hosting:** free. It uses the app database the project already needs, so there is nothing extra to deploy on Azure, a VPS or a home server. The one new dependency is an email-sending service for `IEmailSender`, which every option needs in some form.

### 2. Keycloak (self-hosted)

**Aspire integration.** The hosting package `Aspire.Hosting.Keycloak` runs the `quay.io/keycloak/keycloak` container through `AddKeycloak`. The integration is marked **Preview** ([Aspire Keycloak](https://aspire.dev/integrations/security/keycloak/)). `WithDataVolume()` persists data, and `WithRealmImport` "is intended for local development" and isn't supported for publishing. For production seeding, the docs suggest a custom image, an init service using the Admin REST API, or Terraform ([same page](https://aspire.dev/integrations/security/keycloak/)). Client packages provide `AddKeycloakJwtBearer` for the API and `AddKeycloakOpenIdConnect` for Blazor ([same page](https://aspire.dev/integrations/security/keycloak/)).

**Join Request flow.**

1. The Join Request is stored in the app database.
2. On approval, the API, using a service account with `manage-users`, creates the user through the Admin REST API. It then calls `PUT /admin/realms/{realm}/users/{id}/execute-actions-email` with `["UPDATE_PASSWORD"]`, plus an optional `clientId`, `redirectUri` and `lifespan` ([Keycloak Admin REST API](https://www.keycloak.org/docs-api/latest/rest-api/index.html)). Keycloak sends the email itself, through the realm's SMTP settings ([Server Admin Guide, email](https://www.keycloak.org/docs/latest/server_admin/index.html)). The older `reset-password-email` endpoint is deprecated in favour of this one ([Admin REST API](https://www.keycloak.org/docs-api/latest/rest-api/index.html)). The Admin can also do this by hand: Users, then Credentials, then Credential Reset, then Send Email ([Server Admin Guide](https://www.keycloak.org/docs/latest/server_admin/index.html)).
3. **No open sign-up:** self-registration is a realm toggle (Realm Settings, then Login, then User Registration) that you have to switch on ([Server Admin Guide](https://www.keycloak.org/docs/latest/server_admin/index.html)). Leave it off.

**Front-end fit:** the login UI is Keycloak's own hosted pages over OIDC, so Blazor static SSR and WASM integrate the same way through OIDC. Login and passkey screens live outside the Blazor app and need theming to match.

**Password reset:** toggle "Forgot password" ON ([Server Admin Guide](https://www.keycloak.org/docs/latest/server_admin/index.html)).

**Passkeys:** officially supported from Keycloak 26.4, after a preview that started in 23.0. Enable them under Authentication, then Policies, then Webauthn Passwordless Policy ([Keycloak 26.4 passkeys](https://www.keycloak.org/2025/09/passkeys-support-26-4)).

**Social login:** identity brokering to external OIDC and SAML providers ([Server Admin Guide](https://www.keycloak.org/docs/latest/server_admin/index.html)).

**Cost and hosting:** the software is free, but it is a separate always-on Java service.

- **Memory:** base memory per instance is about **1250 MB of RAM**, and the sizing guide derives a container limit of about 1360 MB ([Keycloak sizing](https://www.keycloak.org/high-availability/multi-cluster/concepts-memory-and-cpu-sizing)).
- **Production requirements:** HTTPS for all traffic, a supported production database, and usually a reverse proxy ([Keycloak production configuration](https://www.keycloak.org/server/configuration-production)).

On a small VPS or home server that is a big share of the machine. Keycloak upgrades and backups also become your job.

### 3. Hosted provider: Microsoft Entra External ID

**Pricing:** billed per monthly active user (MAU), with a free tier; an Azure subscription is required ([External ID pricing](https://learn.microsoft.com/en-us/entra/external-id/external-identities-pricing)). The core offer is free for the first **50,000 MAU** ([External ID FAQ](https://learn.microsoft.com/en-us/entra/external-id/customers/faq-customers); [Entra licensing](https://learn.microsoft.com/en-us/entra/fundamentals/licensing)). Add-ons such as SMS are charged per transaction ([pricing](https://learn.microsoft.com/en-us/entra/external-id/external-identities-pricing)). Cost at 50 Members: **$0**.

**Join Request flow.**

1. The Join Request is stored in the app database.
2. On approval, the API creates the user in the external tenant. Admin-created external users are supported ([External tenant features](https://learn.microsoft.com/en-us/entra/external-id/customers/concept-supported-features-customers)), and "all features ... are also supported for automation through Microsoft Graph" ([same page](https://learn.microsoft.com/en-us/entra/external-id/customers/concept-supported-features-customers)).
3. The *invitation* feature can't be used for this. You "can invite external users for administrative purposes only. You can't use this feature to invite customers to sign in to your apps" ([same page](https://learn.microsoft.com/en-us/entra/external-id/customers/concept-supported-features-customers)). So the app sends its own welcome email linking to sign-in. The Member then sets a password through self-service password reset (email one-time passcode), or signs in with email OTP ([same page, authentication methods table](https://learn.microsoft.com/en-us/entra/external-id/customers/concept-supported-features-customers)). *(The exact first-login experience for admin-created users should be prototyped. This flow is assembled from the documented features, not from a documented "invite" recipe.)*
4. **No open sign-up:** there is no portal toggle. You PATCH the user flow through Microsoft Graph with `isSignUpAllowed: false`, and "you can't read the user flow ID from the Microsoft Entra admin center" ([Disable sign-up](https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-disable-sign-up-user-flow)).

**Front-end fit:** OIDC against `<tenant>.ciamlogin.com` ([External tenant features](https://learn.microsoft.com/en-us/entra/external-id/customers/concept-supported-features-customers)). Microsoft's Blazor docs reference `BlazorWebAppEntra` and `BlazorWebAppEntraBff` samples ([Blazor auth](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-10.0)).

**Password reset, passkeys and social login:** self-service password reset by email OTP or SMS. Passkey (FIDO2) works for sign-in and MFA. Google, Facebook, Apple and custom OIDC federation are supported ([External tenant features](https://learn.microsoft.com/en-us/entra/external-id/customers/concept-supported-features-customers)).

**Hosting:** nothing to run. It ties the project to Azure even if the app ends up on a VPS or home server.

### 4. Hosted provider: Auth0

**Pricing:** the Free plan covers "up to 25,000 monthly active users", with unlimited social connections, one custom domain (needs credit-card verification), "Passwordless Authentication" and five Organizations. MFA factors are "Not available" on Free ([Auth0 pricing](https://auth0.com/pricing)). Cost at 50 Members: **$0**. Free-plan limits can change, which is a vendor risk.

**Join Request flow.**

1. The Join Request is stored in the app database.
2. On approval, the API creates a database user through the Management API, with `email_verified: false` ([Auth0 email invitations](https://auth0.com/docs/customize/email/send-email-invitations-for-application-signup)).
3. The API generates a **password change ticket** through the Management API and emails the link itself, or lets Auth0 send it through the tenant's SMTP provider ([same page](https://auth0.com/docs/customize/email/send-email-invitations-for-application-signup)).
4. **No open sign-up:** turn on "Disable Sign Ups" on the database connection to block sign-ups through public endpoints ([Auth0 Lock configuration](https://auth0.com/docs/libraries/lock/lock-configuration)). Auth0 community posts say this does not cover social connections ([community thread](https://community.auth0.com/t/how-to-disable-sign-up-via-social-login/36843)), so with social login enabled, approval would have to be enforced in the app or in an Action. *(Community source, not official docs.)*

**Front-end fit:** OIDC hosted login, the same integration shape as Keycloak and Entra.

**Hosting:** nothing to run. It adds a third-party vendor.

## Recommendation for KleinGeld Music

Pick **ASP.NET Core Identity** and start from the **Blazor Web App template's Identity pages**:

- Those pages are static SSR, which matches the static SSR front-end option.
- They also work unchanged under global WebAssembly interactivity.
- They include .NET 10 passkeys.

Approving a Join Request then needs only three app operations: create the Member, generate a reset token, and email the link. Leave out the template's Register page. This keeps hosting identical across Azure, a VPS or a home server.

Revisit Keycloak or a hosted provider only if KleinGeld Music later needs single sign-on across several apps.

## Open points

- Choose the email-sending service (SMTP or API). Every option needs one, except where Keycloak or Auth0 send mail themselves, and even they need SMTP configured.
- If the WASM front end is chosen *and* it is a standalone WASM app rather than a Blazor Web App, passkeys need custom endpoints, because `MapIdentityApi` doesn't include them.
