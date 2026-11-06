using KleinGeld.Data;
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

    /// <summary>Email 8: the Admin Rejected a Release, with the reason.</summary>
    public static OutgoingEmail ReleaseRejected(MailboxAddress manager, Release release) =>
        new(manager, $"{release.Title} was rejected", string.Join(Environment.NewLine,
        [
            "Hi,",
            "",
            $"The Admin rejected \"{release.Title}\" by {release.Artist.Name}:",
            "",
            release.RejectionReason!,
            "",
            "You can edit it under My Artists and submit it again.",
        ]));

    /// <summary>Email 3: Forgot password, with the link to choose a new one.</summary>
    public static OutgoingEmail PasswordReset(MailboxAddress member, Uri setPasswordLink) =>
        new(member, "Reset your KleinGeld Music password", string.Join(Environment.NewLine,
        [
            "Hi,",
            "",
            "Someone asked to reset the password for this email address on KleinGeld Music.",
            "Choose a new password here:",
            "",
            setPasswordLink.ToString(),
            "",
            "The link works for 15 minutes. If you didn't ask for this, you can ignore this email.",
        ]));

    /// <summary>Email 4: sent to the new address, with the link that makes it the Member's login email.</summary>
    public static OutgoingEmail ConfirmNewEmail(MailboxAddress newAddress, Uri confirmLink) =>
        new(newAddress, "Confirm your new KleinGeld Music email", string.Join(Environment.NewLine,
        [
            "Hi,",
            "",
            "Someone asked to use this email address to log in to KleinGeld Music.",
            "Confirm it here, and it replaces the old one:",
            "",
            confirmLink.ToString(),
            "",
            "The link works for 15 minutes. If you didn't ask for this, you can ignore this email.",
        ]));

    /// <summary>Email 7: the Admin approved a Release of an Artist the Member manages.</summary>
    public static OutgoingEmail ReleaseApproved(MailboxAddress manager, Release release) =>
        new(manager, $"{release.Title} is Published", string.Join(Environment.NewLine,
        [
            "Hi,",
            "",
            $"The Admin approved \"{release.Title}\" by {release.Artist.Name}. It's Published: every Member can now listen to it.",
        ]));
}
