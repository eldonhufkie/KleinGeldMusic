using KleinGeld.Contracts;
using KleinGeld.Data;
using KleinGeld.Web.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace KleinGeld.Web.Api;

internal static class JoinRequestEndpoints
{
    /// <summary>Endpoints outsiders call, without logging in.</summary>
    public static void MapJoinRequestEndpoints(this RouteGroupBuilder api)
    {
        // Always the same answer, whoever the email belongs to, so the form can't tell anyone who is a Member.
        api.MapPost("/join-requests", async (
            JoinRequestForm form,
            KleinGeldDbContext db,
            UserManager<Member> members,
            ILookupNormalizer normalizer,
            TimeProvider clock,
            EmailSender emails,
            ILoggerFactory loggers) =>
        {
            var email = form.Email.Trim();
            var normalizedEmail = normalizer.NormalizeEmail(email);
            var joinRequest = new JoinRequest
            {
                Name = form.Name.Trim(),
                Email = email,
                NormalizedEmail = normalizedEmail,
                Message = form.Message.Trim(),
                CreatedAt = clock.GetUtcNow(),
                EmailBelongsToMember = await members.Users.AnyAsync(m => m.NormalizedEmail == normalizedEmail),
                DuplicateOfOpenRequest = await db.JoinRequests.AnyAsync(
                    r => r.NormalizedEmail == normalizedEmail && r.Status == JoinRequestStatus.Open),
            };
            db.JoinRequests.Add(joinRequest);
            await db.SaveChangesAsync();

            // The Join Request is stored either way; a failed email must not change the answer.
            try
            {
                foreach (var admin in await members.GetUsersInRoleAsync(Roles.Admin))
                {
                    await emails.SendAsync(AdminEmails.NewJoinRequest(new MailboxAddress(null, admin.Email!), joinRequest));
                }
            }
            catch (Exception exception)
            {
                loggers.CreateLogger(typeof(JoinRequestEndpoints)).LogError(exception, "Couldn't email the Admin about Join Request {JoinRequestId}.", joinRequest.Id);
            }

            return Results.Accepted();
        });
    }
}
