using KleinGeld.Contracts;
using KleinGeld.Data;
using KleinGeld.Web.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace KleinGeld.Web.Api;

internal static class AdminMemberEndpoints
{
    /// <summary>Endpoints the Admin uses to look after Members.</summary>
    public static void MapAdminMemberEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/members/awaiting-password", async (KleinGeldDbContext db) =>
            await db.Users
                .Where(m => m.PasswordHash == null)
                .OrderBy(m => m.Email)
                .Select(m => new MemberAwaitingPassword(m.Id, m.Email!))
                .ToListAsync());

        // Sends email 1 again with a fresh link, for a Member whose link expired before they used it. The link only
        // goes to the Member's inbox, never into the response, so the Admin can't set the password for them.
        api.MapPost("/members/{id:guid}/resend-setup-email", async (
            Guid id,
            HttpRequest request,
            KleinGeldDbContext db,
            UserManager<Member> members,
            EmailSender emails) =>
        {
            var member = await members.FindByIdAsync(id.ToString());
            if (member is null)
            {
                return Results.NotFound();
            }
            if (await members.HasPasswordAsync(member))
            {
                return Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: "This Member has already set a password.");
            }

            // The name the Member gave when asking to join: a Member has no name of their own.
            var name = await db.JoinRequests
                .Where(r => r.NormalizedEmail == member.NormalizedEmail && r.Status == JoinRequestStatus.Approved)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => r.Name)
                .FirstOrDefaultAsync() ?? member.Email!;
            var link = await members.CreateSetPasswordLinkAsync(member, new Uri($"{request.Scheme}://{request.Host}"));
            await emails.SendAsync(MemberEmails.Approved(new MailboxAddress(name, member.Email!), name, link));

            return Results.NoContent();
        });
    }
}
