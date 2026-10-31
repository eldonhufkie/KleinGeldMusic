# Email sending options

Research for ticket `05-email-sending`. All sources read on **2026-10-31**; prices are as shown on that date.

KleinGeld Music sends a small amount of transactional email: Join Request approval/rejection, login setup links, password resets, and possibly the outcome of a Release review (Published / Rejected). Volume is tens of emails per month.

## Summary

| Option | Free tier (as of 2026-10-31) | Cost at our volume | Needs own domain? | .NET | Notes |
| --- | --- | --- | --- | --- | --- |
| **Resend** | 3,000/month, max 100/day, 3 domains [1] | $0 | Yes, at least one verified domain [2] | Official `Resend` NuGet SDK [3]; SMTP on 25/465/587/2465/2587 [4] | Best fit |
| **Postmark** | 100/month, no overage allowed [5] | $0 (tight); $15/mo Basic beyond that [5] | Yes. Account approval needed before sending outside your own domain [6] | Official `Postmark` NuGet [7]; SMTP on 25/587/2525 [8] | Strong on deliverability, small free tier |
| **Amazon SES** | No standing free tier; new-account credits up to $200 [9] | ~$0.10 per 1,000 emails, so effectively $0 [9] | Verify the From identity; sandbox until production access is granted [10] | AWS SDK; SMTP on 25/587/2587/465/2465 [11] | Cheapest at scale, most setup work |
| **Brevo** | 300/day (vendor marketing; pricing page could not be read directly) [12] | $0 | Yes, for good deliverability | SMTP relay plus API [12] | Mainly a marketing-email product |
| **Mailgun** | 100/day, 1 custom domain, 1-day log retention [13] | $0 | Sandbox domain only reaches 5 authorized recipients [14] | SMTP/HTTP | Usable, but logs are very short |
| **Azure Communication Services Email** | None; pay as you go at $0.00025/email + $0.00012/MB [15] | Fractions of a cent | Free Azure managed domain is capped at 10 emails/hour, with no increase available [16] | Official `Azure.Communication.Email` SDK [17]; SMTP relay at `smtp.azurecomm.net` [18] | **Being retired 2028-09-30. Microsoft advises against new onboarding** [19] |
| **Own SMTP from a VPS or home server** | n/a | $0 plus a lot of operations work | Yes, plus PTR/rDNS | MailKit | Port 25 blocked by Azure, Hetzner and DigitalOcean by default; residential IPs are on blocklists [20][21][22][23] |

### Recommendation

1. **Send through Resend over SMTP using MailKit**, from an address on a domain we own (for example `noreply@mail.<our-domain>`). Publish the SPF and DKIM records Resend gives us, plus a DMARC record with `p=none` to start. The free tier (3,000/month, 100/day) is about 100× our volume, and it works the same whichever hosting option we pick.
2. **Hide sending behind a single email-sending seam** in the API, configured only by an SMTP connection string. In development, Aspire points it at a **MailPit** container (`CommunityToolkit.Aspire.Hosting.MailPit`) [24][25]. In production, it points at the provider's SMTP host. With plain SMTP, changing provider (Postmark, SES) is a configuration change, not a code change.
3. **Do not choose Azure Communication Services Email**, even if we host on Azure: it is in maintenance mode and retires on 2028-09-30 [19].
4. **Do not send directly from our own server** (see below).
5. If hosting ends up on DigitalOcean, use Resend's port 2587/2465 (or Postmark's 2525, or an HTTP API), because DigitalOcean blocks 25, 465 **and** 587 [22].

Fallback: Postmark if we want its deliverability reputation and stay under 100 emails/month; Amazon SES if volume ever grows.

## Detail

### Cost at our volume

- **Resend**: the Free plan gives 3,000 emails/month, "limited to 100 emails per day", 3 domains and 30-day data retention. Pro is $20/mo for 50,000 [1].
- **Postmark**: the free plan gives "100 emails/month" with no overages. Basic is $15.00/mo, "starting at 10,000 emails/month" [5]. Tens per month fits, but there is little headroom.
- **Amazon SES**: à la carte pricing is $0.10 per 1,000 outbound emails, and attachments cost $0.12/GB. New customers get up to $200 in AWS Free Tier credits, usable for 6 months on the free plan [9]. At ~50 emails/month the cost rounds to zero.
- **Brevo**: 300 emails/day free, with SMTP relay and API access, once Brevo approves the account. Starter is from $9/mo [12]. Brevo's pricing page renders client-side and could not be read directly, so these figures come from Brevo's own pages via search and should be confirmed in its dashboard.
- **Mailgun**: free plan of "100 emails/day", 1 custom sending domain, and "1 day log retention". Basic is from $15/mo for 10,000/mo [13].
- **Azure Communication Services Email**: $0.00025 per email plus $0.00012 per MB transferred, with no free tier [15]. 50 emails a month costs about $0.0125.

### Deliverability: custom domain, SPF/DKIM/DMARC

- Gmail requires **all** senders to set up SPF *or* DKIM, have valid forward and reverse DNS (PTR), use TLS, and keep the spam rate under 0.3%. Senders over 5,000/day also need SPF *and* DKIM, DMARC, and From-alignment [26]. We are far below bulk volume, but SPF + DKIM + DMARC (`p=none`) is cheap to set up and a good habit.
- With an email service provider, the provider owns the sending IPs and their PTR records. We only publish DNS records for our domain.
- **Without a custom domain:**
  - Resend: "You must add and verify at least one domain to send emails" [2].
  - Postmark: the From address must be on a verified domain or sender signature. It won't let you send as Gmail or Yahoo addresses (that counts as spoofing), and an account pending approval can only send to its own verified domains [6].
  - Amazon SES: starts in a sandbox where you can only send *to* verified addresses, max 200 per 24 h and 1 per second. Even in production, From/Return-Path identities must be verified [10].
  - Mailgun: the sandbox domain only delivers to up to 5 authorized recipients [14].
  - Azure Communication Services: offers a free managed `...azurecomm.net` domain, preconfigured for SPF/DKIM [16a]. It is limited to **5 emails/minute and 10/hour per subscription, with no higher limits available**. Higher quotas are "only available for verified custom domains" [16].
- **Conclusion:** we need a domain we control for production email whichever provider we pick. That is probably needed for the site anyway.

### .NET integration and local development

- **Use MailKit, not `System.Net.Mail.SmtpClient`.** Microsoft's API docs say: "We don't recommend that you use the `SmtpClient` class for new development... Use MailKit or other libraries instead" [27]. MailKit is MIT-licensed and targets .NET 8+ / .NET Standard 2.0 [28].
- **Provider SDKs** (an alternative to SMTP): `Resend` [3], `Postmark` [7], `Azure.Communication.Email` [17]. Using SMTP keeps us provider-neutral and lets us use the same code path against MailPit.
- **Provider SMTP endpoints:**
  - Resend: `smtp.resend.com`, username `resend`, password is the API key, ports 25/465/587/2465/2587 [4].
  - Postmark: `smtp.postmarkapp.com`, ports 25/2525/587 [8].
  - Amazon SES: STARTTLS on 25/587/2587, TLS wrapper on 465/2465 [11].
  - Azure Communication Services: `smtp.azurecomm.net:587`, authenticated with an Entra app client secret [18].
- **MailPit** is an MIT-licensed email-testing SMTP server with a web UI. It listens for SMTP on port 1025 and serves the UI on 8025 (image `axllent/mailpit`) [29].
- **MailPit in Aspire:** `CommunityToolkit.Aspire.Hosting.MailPit` provides `builder.AddMailPit("mailpit")`, referenced from the API with `.WithReference(mailpit)` [24]. The API then receives `ConnectionStrings__mailpit=Endpoint=smtp://localhost:1025`, plus `MAILPIT_HOST` and `MAILPIT_PORT` [25]. It is hosting-only, with no client package; you use any SMTP library [30]. Aspire's own custom-client-integration tutorial builds a MailKit client against MailPit, which is a usable template [31].

### Why not send directly from a home server or VPS

- **Port 25 is blocked by default on most clouds:**
  - Azure blocks outbound TCP 25 from VMs on all subscription types except Enterprise Agreement / MCA-E. Microsoft recommends an authenticated SMTP relay on port 587 instead, "in part to maintain IP reputation" [20].
  - Hetzner Cloud blocks 25 and 465 by default. Unblocking requires an account at least a month old with a paid invoice, plus a case-by-case request. Port 587 to external services is open [21].
  - DigitalOcean blocks 25, 465 and 587 on Droplets and recommends a third-party email service [22].
  - AWS EC2 throttles port 25 by default [11].
- **Residential IPs are on blocklists:** the Spamhaus PBL lists "end-user IP address ranges from which email should never be sent directly to the final destination", typically ISP broadband ranges, covering about 40% of routable IPv4 space [23]. Mail from a home server's IP would be widely rejected.
- **Reverse DNS:** Gmail requires a PTR record that matches the sending IP [26]. Home connections usually can't set one, and VPSs need extra setup.
- **Reputation and operations:** a new IP has no sending reputation, and we would own bounces, blocklist monitoring and TLS ourselves. Even where port 25 is open, Azure says "there's no guarantee that external domains accept the incoming emails" [20].

None of this blocks us from *relaying* through a provider from any host, over 587 (or 2587/2525 on DigitalOcean) or HTTPS.

### Azure Communication Services Email retirement

Microsoft's retirement guide (updated 2026-10-29) lists ACS Email as "retiring". It is in "maintenance mode — security and critical fixes only" and supported until **2028-09-30**. Microsoft writes: "We recommend using the two-year retirement period to migrate existing workloads off ACS Email rather than onboarding new solutions." The suggested Microsoft replacements (M365 High Volume Email, Exchange Online) need Microsoft 365 licensing, and HVE is internal-send only [19]. This removes the one advantage it had if we host on Azure.

## Sources

All read 2026-10-31.

1. Resend pricing: https://resend.com/pricing
2. Resend, Domains: https://resend.com/docs/dashboard/domains/introduction
3. Resend, Send with .NET: https://resend.com/docs/send-with-dotnet
4. Resend, Send with SMTP: https://resend.com/docs/send-with-smtp
5. Postmark pricing: https://postmarkapp.com/pricing
6. Postmark, account approval process: https://postmarkapp.com/support/article/1084-how-does-the-account-approval-process-work; Why you can't use a Gmail address: https://postmarkapp.com/blog/why-cant-i-use-gmail-address
7. Postmark official libraries: https://postmarkapp.com/developer/integration/official-libraries
8. Postmark, Send email with SMTP: https://postmarkapp.com/developer/user-guide/send-email-with-smtp
9. Amazon SES pricing: https://aws.amazon.com/ses/pricing/
10. Amazon SES, Request production access (sandbox): https://docs.aws.amazon.com/ses/latest/dg/request-production-access.html
11. Amazon SES, Connecting to an SMTP endpoint: https://docs.aws.amazon.com/ses/latest/dg/smtp-connect.html
12. Brevo pricing / free SMTP (via search of brevo.com; page not directly readable): https://www.brevo.com/pricing/, https://www.brevo.com/free-smtp-server/
13. Mailgun pricing: https://www.mailgun.com/pricing/
14. Mailgun, Sandbox domains: https://documentation.mailgun.com/docs/mailgun/user-manual/domains/domains-sandbox
15. Microsoft Learn, ACS Email pricing: https://learn.microsoft.com/en-us/azure/communication-services/concepts/email-pricing (the Azure pricing page at https://azure.microsoft.com/en-us/pricing/details/communication-services/ rendered without figures)
16. Microsoft Learn, ACS service limits (Email): https://learn.microsoft.com/en-us/azure/communication-services/concepts/service-limits
16a. Microsoft Learn, Email domains and sender authentication: https://learn.microsoft.com/en-us/azure/communication-services/concepts/email/email-domain-and-sender-authentication
17. Microsoft Learn, Send an email (C#): https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/send-email?pivots=programming-language-csharp
18. Microsoft Learn, ACS SMTP authentication: https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/send-email-smtp/smtp-authentication
19. Microsoft Learn, ACS retirement and breaking changes guide: https://learn.microsoft.com/en-us/azure/communication-services/acs-retirement-and-breaking-changes-guide
20. Microsoft Learn, Troubleshoot outbound SMTP connectivity in Azure: https://learn.microsoft.com/en-us/azure/virtual-network/troubleshoot-outbound-smtp-connectivity
21. Hetzner Cloud server FAQ: https://docs.hetzner.com/cloud/servers/faq/
22. DigitalOcean, Why is SMTP blocked: https://docs.digitalocean.com/support/why-is-smtp-blocked/
23. Spamhaus Policy Blocklist: https://www.spamhaus.org/blocklists/policy-blocklist/
24. Aspire, MailPit hosting: https://aspire.dev/integrations/devtools/mailpit/mailpit-host/
25. Aspire, Connect to MailPit: https://aspire.dev/integrations/devtools/mailpit/mailpit-connect/
26. Google, Email sender guidelines: https://support.google.com/a/answer/81126
27. Microsoft Learn, SmtpClient class remarks: https://learn.microsoft.com/en-us/dotnet/api/system.net.mail.smtpclient
28. MailKit repository: https://github.com/jstedfast/MailKit
29. Mailpit repository: https://github.com/axllent/mailpit
30. Aspire, MailPit get started: https://aspire.dev/integrations/devtools/mailpit/mailpit-get-started/
31. Aspire, Create custom client integrations (MailKit + MailPit): https://aspire.dev/integrations/custom-integrations/client-integrations/
