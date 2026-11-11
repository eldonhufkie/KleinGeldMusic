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
    /// <summary>Endpoints for managing Members to send a Release to the Admin's review, take it back, and Take down a Published one.</summary>
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
                if (release.Stage is not (ReleaseStage.Draft or ReleaseStage.Rejected or ReleaseStage.TakenDown))
                {
                    return Results.Problem("Only a Draft, Rejected or Taken down Release can be submitted.", statusCode: StatusCodes.Status409Conflict);
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
                await db.SaveChangesAsync();
                submitted = release;
                return null;
            });
            if (refused is not null)
            {
                return refused;
            }

            // The Release is Submitted whatever happens here: the Review page shows it even if the email fails.
            await EmailAdminAsync(submitted!, AdminEmails.ReleaseSubmitted, db, members, emails, loggers);

            return Results.Ok(submitted!.ToDetails(covers));
        });

        api.MapPost("/releases/{id:guid}/pending-changes/submit", async (
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
                if (release.PendingChanges is not (ReleaseStage.Draft or ReleaseStage.Rejected))
                {
                    return Results.Problem("Only Draft or Rejected Pending Changes can be submitted.", statusCode: StatusCodes.Status409Conflict);
                }
                if (release.Tracks.Any(t => (t.Pending || t.IsReplaced) && t.Status != TrackStatus.Ready))
                {
                    return Results.Problem(
                        "Wait until every added or replaced Track is Ready, and replace or remove any that Failed.",
                        statusCode: StatusCodes.Status409Conflict);
                }

                db.RightsDeclarations.Add(new RightsDeclaration
                {
                    ReleaseId = release.Id,
                    MemberId = caller.MemberId(),
                    MadeAt = clock.GetUtcNow(),
                    WordingVersion = RightsDeclarationWording.Version,
                });
                release.PendingChanges = ReleaseStage.Submitted;
                await db.SaveChangesAsync();
                submitted = release;
                return null;
            });
            if (refused is not null)
            {
                return refused;
            }

            // The Pending Changes are Submitted whatever happens here, as with a Release.
            await EmailAdminAsync(submitted!, AdminEmails.PendingChangesSubmitted, db, members, emails, loggers);

            return Results.Ok(submitted!.ToDetails(covers));
        });

        api.MapPost("/releases/{id:guid}/pending-changes/withdraw", async (Guid id, ClaimsPrincipal caller, KleinGeldDbContext db, CoverStore covers) =>
        {
            Release? withdrawn = null;
            var refused = await db.WithManagedReleaseLockedAsync(id, caller, async release =>
            {
                if (release.PendingChanges != ReleaseStage.Submitted)
                {
                    return Results.Problem("Only Submitted Pending Changes can be withdrawn.", statusCode: StatusCodes.Status409Conflict);
                }

                release.PendingChanges = ReleaseStage.Draft;
                await db.SaveChangesAsync();
                withdrawn = release;
                return null;
            });
            return refused ?? Results.Ok(withdrawn!.ToDetails(covers));
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

        api.MapPost("/releases/{id:guid}/takedown", async (
            Guid id, ClaimsPrincipal caller, KleinGeldDbContext db, CoverStore covers, PlayLinks links, BlobLinks blobs) =>
        {
            Release? takenDown = null;
            IReadOnlyList<string> unusedBlobs = [];
            var refused = await db.WithManagedReleaseLockedAsync(id, caller, async release =>
            {
                if (release.Stage != ReleaseStage.Published)
                {
                    return Results.Problem("Only a Published Release can be taken down.", statusCode: StatusCodes.Status409Conflict);
                }

                // Any Pending Changes fold into the Release, which is then reviewed whole on resubmission.
                unusedBlobs = release.MergePendingChanges();
                release.Stage = ReleaseStage.TakenDown;
                await db.SaveChangesAsync();
                takenDown = release;
                return null;
            });
            if (refused is not null)
            {
                return refused;
            }

            // After the commit, so a link fetched meanwhile is either refused or signed against the policy that goes.
            await links.RevokeAllAsync();
            foreach (var blobName in unusedBlobs)
            {
                await blobs.Blob(Track.AudioContainer, blobName).DeleteIfExistsAsync();
            }
            return Results.Ok(takenDown!.ToDetails(covers));
        });
    }

    /// <summary>Email 6 to the Admin, unless the Admin manages the Artist and so knows. A failure is only logged.</summary>
    private static async Task EmailAdminAsync(
        Release submitted,
        Func<MailboxAddress, Release, OutgoingEmail> email,
        KleinGeldDbContext db,
        UserManager<Member> members,
        EmailSender emails,
        ILoggerFactory loggers)
    {
        try
        {
            var managerIds = await db.Artists
                .Where(a => a.Id == submitted.ArtistId)
                .SelectMany(a => a.Managers)
                .Select(m => m.Id)
                .ToListAsync();
            var admins = await members.GetUsersInRoleAsync(Roles.Admin);

            // The Admin's own Artist: they know, whichever manager pressed Submit.
            foreach (var admin in admins.Any(a => managerIds.Contains(a.Id)) ? [] : admins)
            {
                await emails.SendAsync(email(new MailboxAddress(null, admin.Email!), submitted));
            }
        }
        catch (Exception exception)
        {
            loggers.CreateLogger(typeof(SubmissionEndpoints)).LogError(exception, "Couldn't email the Admin about Release {ReleaseId}.", submitted.Id);
        }
    }
}
