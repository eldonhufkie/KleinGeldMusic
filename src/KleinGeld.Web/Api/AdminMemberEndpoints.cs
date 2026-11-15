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

        api.MapGet("/members", async (KleinGeldDbContext db) =>
        {
            var adminIds = db.AdminIds();
            return await db.Users
                .OrderBy(m => m.Email)
                .Select(m => new MemberListing(m.Id, m.Email!, adminIds.Contains(m.Id)))
                .ToListAsync();
        });

        // What Removing the Member would delete: the same confirmation as Leave.
        api.MapGet("/members/{id:guid}/remove", async (Guid id, KleinGeldDbContext db) =>
            await db.Users.AnyAsync(m => m.Id == id) ? Results.Ok(await db.PreviewAsync(id)) : Results.NotFound());

        // Removing a Member with a reason: deleted as on Leave, cut off at once by the security stamp check, then
        // email 10 with the reason and the Artists that went with them. No email 9 for those Artists.
        api.MapPost("/members/{id:guid}/remove", async (
            Guid id, RemoveForm form, KleinGeldDbContext db, [AsParameters] RemovalStores stores,
            EmailSender emails, ILoggerFactory loggers) =>
        {
            var logger = loggers.CreateLogger(typeof(AdminMemberEndpoints));
            var result = await db.DeleteAsync(stores, logger, id);
            switch (result.Outcome)
            {
                case MemberDeletionOutcome.IsAdmin:
                    return Results.Problem("The Admin can't be Removed.", statusCode: StatusCodes.Status409Conflict);
                case MemberDeletionOutcome.NotFound:
                    return Results.NotFound();
            }
            var deleted = result.Deleted!;

            try
            {
                await emails.SendAsync(MemberEmails.Removed(new MailboxAddress(null, deleted.Email), form.Reason.Trim(), deleted.Artists));
            }
            catch (Exception exception)
            {
                // The Removal stands whatever happens here.
                logger.LogError(exception, "Couldn't email Removed Member {Id}.", id);
            }
            return Results.NoContent();
        });

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
