# Email sending options

Type: research
Status: resolved
Blocked by: —

## Question

KleinGeld Music must send a small amount of transactional email: Join Request approval/rejection, login setup links, password resets, and possibly Release review outcomes. Volume is tiny (tens per month).

Which providers fit? Compare e.g. Azure Communication Services Email, Amazon SES, Postmark, Resend, Brevo, Mailgun, and plain SMTP from a VPS/home server:
- Free tier and cost at our volume.
- Deliverability requirements (custom domain, SPF/DKIM/DMARC setup) and what happens without a custom domain.
- .NET integration (official SDK, SMTP via MailKit) and local development (e.g. a fake SMTP server such as MailPit as an Aspire resource).
- Why sending directly from a home server or VPS is usually a bad idea (IP reputation, blocked port 25), if it is.

## Answer

**Use Resend over SMTP (via MailKit), sending from a domain we own.** Its free tier (3,000/month, 100/day) far exceeds our volume; we publish its SPF/DKIM records plus a DMARC record. Keep the code provider-neutral: one email seam configured by an SMTP connection string, pointed at MailPit locally through Aspire. Fallbacks: Postmark or Amazon SES. **Ruled out:** Azure Communication Services Email (retiring 2028-09-30, in maintenance mode) and sending directly from our own server (port 25 blocked by Azure/Hetzner/DigitalOcean; home IPs blocklisted). A custom domain is needed for production email whichever provider we pick.

Findings, with citations: `docs/research/email-sending.md` on branch `research/email-sending` (pushed to origin; never merged).
