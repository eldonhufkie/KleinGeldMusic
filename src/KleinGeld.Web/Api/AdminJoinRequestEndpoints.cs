using KleinGeld.Contracts;
using KleinGeld.Data;
using KleinGeld.Web.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace KleinGeld.Web.Api;

internal static class AdminJoinRequestEndpoints
{
    /// <summary>Endpoints the Admin uses to handle Join Requests.</summary>
    public static void MapAdminJoinRequestEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/join-requests", async (KleinGeldDbContext db) =>
            await db.JoinRequests
                .Where(r => r.Status == JoinRequestStatus.Open)
                .OrderBy(r => r.CreatedAt)
                .Select(r => new OpenJoinRequest(
                    r.Id, r.Name, r.Email, r.Message, r.CreatedAt, r.EmailBelongsToMember, r.DuplicateOfOpenRequest))
                .ToListAsync());

        // Creates a passwordless Member with the email confirmed (the person proved the inbox is theirs by
        // asking from it, and again by using the link), then sends email 1 with the set-password link.
        api.MapPost("/join-requests/{id:guid}/approve", async (
            Guid id,
            HttpRequest request,
            KleinGeldDbContext db,
            UserManager<Member> members,
            EmailSender emails) =>
        {
            var joinRequest = await db.JoinRequests.SingleOrDefaultAsync(r => r.Id == id && r.Status == JoinRequestStatus.Open);
            if (joinRequest is null)
            {
                return Results.NotFound();
            }

            // Changed before the Member is created: Identity's save stores both together, or neither.
            joinRequest.Status = JoinRequestStatus.Approved;
            var member = new Member { UserName = joinRequest.Email, Email = joinRequest.Email, EmailConfirmed = true };
            var created = await members.CreateAsync(member);
            if (!created.Succeeded)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    detail: string.Join(" ", created.Errors.Select(error => error.Description)));
            }

            var link = await members.CreateSetPasswordLinkAsync(member, new Uri($"{request.Scheme}://{request.Host}"));
            await emails.SendAsync(MemberEmails.Approved(new MailboxAddress(joinRequest.Name, member.Email), joinRequest.Name, link));

            return Results.NoContent();
        });

        // Sends email 2 whatever the flags say: the Admin never gives a reason, so nothing in it reveals one.
        api.MapPost("/join-requests/{id:guid}/reject", async (Guid id, KleinGeldDbContext db, EmailSender emails) =>
        {
            var joinRequest = await db.JoinRequests.SingleOrDefaultAsync(r => r.Id == id && r.Status == JoinRequestStatus.Open);
            if (joinRequest is null)
            {
                return Results.NotFound();
            }

            joinRequest.Status = JoinRequestStatus.Rejected;
            await db.SaveChangesAsync();
            await emails.SendAsync(MemberEmails.Rejected(new MailboxAddress(joinRequest.Name, joinRequest.Email), joinRequest.Name));

            return Results.NoContent();
        });
    }
}
