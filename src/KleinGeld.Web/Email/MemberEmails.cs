using MimeKit;

namespace KleinGeld.Web.Email;

/// <summary>The emails sent to Members and to people asking to become one.</summary>
internal static class MemberEmails
{
    /// <summary>Email 1: the Join Request was approved, with the link to set a password.</summary>
    public static OutgoingEmail Approved(MailboxAddress member, string name, Uri setPasswordLink) =>
        new(member, "Welcome to KleinGeld Music", string.Join(Environment.NewLine,
        [
            $"Hi {name},",
            "",
            "Your Join Request was approved: you're now a Member of KleinGeld Music.",
            "Set your password here, then log in with this email address:",
            "",
            setPasswordLink.ToString(),
            "",
            "The link works for 15 minutes. If it has expired, use \"Forgot your password?\" on the Login page,",
            "or ask the Admin to send this email again.",
        ]));

    /// <summary>Email 2: the Join Request was rejected. Short and polite, and never with a reason.</summary>
    public static OutgoingEmail Rejected(MailboxAddress person, string name) =>
        new(person, "Your Join Request to KleinGeld Music", string.Join(Environment.NewLine,
        [
            $"Hi {name},",
            "",
            "Thank you for asking to join KleinGeld Music. Unfortunately the Admin couldn't accept your Join Request.",
            "",
            "Kind regards,",
            "KleinGeld Music",
        ]));
}
