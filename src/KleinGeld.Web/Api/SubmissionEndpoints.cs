using System.Security.Claims;
using KleinGeld.Contracts;
using KleinGeld.Data;
using KleinGeld.Web.Blobs;

namespace KleinGeld.Web.Api;

internal static class SubmissionEndpoints
{
    /// <summary>Endpoints for managing Members to send a Release to the Admin's review and take it back.</summary>
    public static void MapSubmissionEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/releases/{id:guid}/submit", async (Guid id, SubmitForm form, ClaimsPrincipal caller, KleinGeldDbContext db, CoverStore covers) =>
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

                release.Stage = ReleaseStage.Submitted;
                await db.SaveChangesAsync();
                submitted = release;
                return null;
            });
            return refused ?? Results.Ok(submitted!.ToDetails(covers));
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
