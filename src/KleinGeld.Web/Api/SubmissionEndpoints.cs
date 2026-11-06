using System.Security.Claims;
using KleinGeld.Contracts;
using KleinGeld.Data;
using KleinGeld.Web.Blobs;
using KleinGeld.Web.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace KleinGeld.Web.Api;

internal static class SubmissionEndpoints
{
    /// <summary>Endpoints for managing Members to send a Release to the Admin's review and take it back.</summary>
    public static void MapSubmissionEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/releases/{id:guid}/submit", async (
            Guid id,
            SubmitForm form,
            ClaimsPrincipal caller,
            KleinGeldDbContext db,
            CoverStore covers,
            TimeProvider clock,
            UserManager<Member> members,
            EmailSender emails,
            ILoggerFactory loggers) =>
        {
            if (!form.RightsDeclared)
            {
                return Results.Problem("Tick the Rights Declaration to submit.", statusCode: StatusCodes.Status400BadRequest);
            }

            Release? submitted = null;
            var refused = await db.WithManagedReleaseLockedAsync(id, caller, async release =>
            {
                if (release.Stage is not (ReleaseStage.Draft or ReleaseStage.Rejected))
                {
                    return Results.Problem("Only a Draft or Rejected Release can be submitted.", statusCode: StatusCodes.Status409Conflict);
                }

                var tracks = release.Tracks.Select(t => t.Status).ToList();
                if (SubmitRules.Refusal(release.Title, release.ReleaseDate, release.CoverId is not null, tracks) is { } refusal)
                {
                    return Results.Problem(refusal, statusCode: StatusCodes.Status409Conflict);
                }

                db.RightsDeclarations.Add(new RightsDeclaration
                {
                    ReleaseId = release.Id,
                    MemberId = caller.MemberId(),
                    MadeAt = clock.GetUtcNow(),
                    WordingVersion = RightsDeclarationWording.Version,
                });
                release.Stage = ReleaseStage.Submitted;
                release.RejectionReason = null;
                await db.SaveChangesAsync();
                submitted = release;
                return null;
            });
            if (refused is not null)
            {
                return refused;
            }

            // The Release is Submitted whatever happens here: the Review page shows it even if the email fails.
            try
            {
                var managerIds = await db.Artists
                    .Where(a => a.Id == submitted!.ArtistId)
                    .SelectMany(a => a.Managers)
                    .Select(m => m.Id)
                    .ToListAsync();
                var admins = await members.GetUsersInRoleAsync(Roles.Admin);

                // The Admin's own Artist: they know, whichever manager pressed Submit.
                foreach (var admin in admins.Any(a => managerIds.Contains(a.Id)) ? [] : admins)
                {
                    await emails.SendAsync(AdminEmails.ReleaseSubmitted(new MailboxAddress(null, admin.Email!), submitted!));
                }
            }
            catch (Exception exception)
            {
                loggers.CreateLogger(typeof(SubmissionEndpoints)).LogError(exception, "Couldn't email the Admin about Release {ReleaseId}.", id);
            }

            return Results.Ok(submitted!.ToDetails(covers));
        });

        api.MapPost("/releases/{id:guid}/withdraw", async (Guid id, ClaimsPrincipal caller, KleinGeldDbContext db, CoverStore covers) =>
        {
            Release? withdrawn = null;
            var refused = await db.WithManagedReleaseLockedAsync(id, caller, async release =>
            {
                if (release.Stage != ReleaseStage.Submitted)
                {
                    return Results.Problem("Only a Submitted Release can be withdrawn.", statusCode: StatusCodes.Status409Conflict);
                }

                release.Stage = ReleaseStage.Draft;
                await db.SaveChangesAsync();
                withdrawn = release;
                return null;
            });
            return refused ?? Results.Ok(withdrawn!.ToDetails(covers));
        });
    }
}
