using KleinGeld.Data;
using MimeKit;

namespace KleinGeld.Web.Email;

/// <summary>The emails sent to the Admin.</summary>
internal static class AdminEmails
{
    /// <summary>Email 5: a new Join Request arrived.</summary>
    public static OutgoingEmail NewJoinRequest(MailboxAddress admin, JoinRequest joinRequest)
    {
        List<string> lines =
        [
            $"{joinRequest.Name} <{joinRequest.Email}> asked to join KleinGeld Music:",
            "",
            joinRequest.Message,
            "",
        ];
        if (joinRequest.EmailBelongsToMember)
        {
            lines.Add("This email already belongs to a Member.");
        }
        if (joinRequest.DuplicateOfOpenRequest)
        {
            lines.Add("There is already an open Join Request from this email.");
        }
        lines.Add("Approve or reject it on the Admin page.");

        return new(admin, $"New Join Request from {joinRequest.Name}", string.Join(Environment.NewLine, lines));
    }
}
