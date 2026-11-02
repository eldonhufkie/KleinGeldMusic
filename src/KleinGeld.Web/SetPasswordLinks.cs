using System.Text;
using KleinGeld.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace KleinGeld.Web;

internal static class SetPasswordLinks
{
    /// <summary>
    /// A link to the Set your password page that works once, for 15 minutes. Emails 1 and 3 both use it: setting a
    /// first password and resetting a forgotten one are the same reset token.
    /// </summary>
    public static async Task<Uri> CreateSetPasswordLinkAsync(this UserManager<Member> members, Member member, Uri origin)
    {
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await members.GeneratePasswordResetTokenAsync(member)));
        return new Uri(QueryHelpers.AddQueryString(
            new Uri(origin, "/Account/SetUpPassword").ToString(),
            new Dictionary<string, string?> { ["memberId"] = member.Id.ToString(), ["code"] = code }));
    }
}
