using KleinGeld.Contracts;
using KleinGeld.Data;
using KleinGeld.Web.Blobs;
using KleinGeld.Web.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace KleinGeld.Web.Api;

internal static class AdminReviewEndpoints
{
    /// <summary>Endpoints for the Admin's Review page.</summary>
    public static void MapAdminReviewEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/review/count", (KleinGeldDbContext db) => db.AwaitingReviewCountAsync());

        api.MapGet("/review", async (KleinGeldDbContext db) =>
        {
            var releases = await db.Releases
                .Include(r => r.Artist)
                .Include(r => r.RightsDeclarations)
                .Where(r => r.Stage == ReleaseStage.Submitted)
                .ToListAsync();

            var memberIds = releases.SelectMany(r => r.RightsDeclarations).Select(d => d.MemberId).ToHashSet();
            var names = (await db.Users.Where(m => memberIds.Contains(m.Id)).WithNamesAsync(db)).ToDictionary(m => m.Id, m => m.Name);

            return releases
                .Select(r =>
                {
                    var declarations = r.RightsDeclarations
                        .OrderByDescending(d => d.MadeAt)
                        .Select(d => new MadeRightsDeclaration(
                            d.MemberId is { } id ? names.GetValueOrDefault(id) : null, d.MadeAt, d.WordingVersion))
                        .ToList();
                    return new ReviewRelease(r.Id, r.Title, r.Slug, r.Artist.Name, r.Type, declarations[0].MadeAt, declarations);
                })
                .OrderBy(r => r.SubmittedAt)
                .ToList();
        });

        api.MapPost("/releases/{id:guid}/reject", async (
            Guid id,
            RejectForm form,
            KleinGeldDbContext db,
            CoverStore covers,
            UserManager<Member> members,
            EmailSender emails,
            ILoggerFactory loggers) =>
        {
            Release? rejected = null;
            var refused = await db.WithReviewedReleaseLockedAsync(id, async release =>
            {
                if (release.Stage != ReleaseStage.Submitted)
                {
                    return Results.Problem("Only a Submitted Release can be rejected.", statusCode: StatusCodes.Status409Conflict);
                }

                release.Stage = ReleaseStage.Rejected;
                release.RejectionReason = form.Reason.Trim();
                await db.SaveChangesAsync();
                rejected = release;
                return null;
            });
            if (refused is not null)
            {
                return refused;
            }

            // The Release is Rejected whatever happens here: My Artists shows the reason even if the email fails.
            try
            {
                var adminIds = (await members.GetUsersInRoleAsync(Roles.Admin)).Select(a => a.Id).ToList();
                var managerEmails = await db.Artists
                    .Where(a => a.Id == rejected!.ArtistId)
                    .SelectMany(a => a.Managers)
                    .Where(m => !adminIds.Contains(m.Id))
                    .Select(m => m.Email!)
                    .ToListAsync();

                foreach (var email in managerEmails)
                {
                    await emails.SendAsync(MemberEmails.ReleaseRejected(new MailboxAddress(null, email), rejected!));
                }
            }
            catch (Exception exception)
            {
                loggers.CreateLogger(typeof(AdminReviewEndpoints)).LogError(exception, "Couldn't email the managers of Release {ReleaseId}.", id);
            }

            return Results.Ok(rejected!.ToDetails(covers));
        });
    }

    /// <summary>How much is waiting on the Review page, which the badge on the Admin link shows.</summary>
    public static Task<int> AwaitingReviewCountAsync(this KleinGeldDbContext db) =>
        db.Releases.CountAsync(r => r.Stage == ReleaseStage.Submitted);
}
