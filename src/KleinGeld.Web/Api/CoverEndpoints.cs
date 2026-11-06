using System.Security.Claims;
using KleinGeld.Contracts;
using KleinGeld.Data;
using KleinGeld.Web.Blobs;
using Microsoft.EntityFrameworkCore;

namespace KleinGeld.Web.Api;

internal static class CoverEndpoints
{
    /// <summary>Endpoints for managing Members to upload a Release's cover art straight to blob storage.</summary>
    public static void MapCoverEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/releases/{id:guid}/cover/upload-link", async (Guid id, ClaimsPrincipal caller, KleinGeldDbContext db, CoverStore covers) =>
        {
            var refused = await db.WithUnsubmittedReleaseLockedAsync(id, caller, _ => Task.FromResult<IResult?>(null));
            return refused ?? Results.Ok(await covers.CreateUploadLinkAsync(id));
        });

        api.MapPost("/releases/{id:guid}/cover", async (Guid id, ConfirmUploadForm form, ClaimsPrincipal caller, KleinGeldDbContext db, CoverStore covers) =>
        {
            // Checked before the slow image work, and again under the lock: a Submit may land in between, and then
            // the accepted upload is an orphan for the clean-up.
            var refused = await db.WithUnsubmittedReleaseLockedAsync(id, caller, _ => Task.FromResult<IResult?>(null));
            if (refused is not null)
            {
                return refused;
            }

            var refusal = await covers.AcceptAsync(id, form.UploadId!.Value);
            if (refusal is not null)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, detail: refusal);
            }

            Release? changed = null;
            refused = await db.WithUnsubmittedReleaseLockedAsync(id, caller, async release =>
            {
                release.CoverId = form.UploadId;
                await db.SaveChangesAsync();
                changed = release;
                return null;
            });
            return refused ?? Results.Ok(changed!.ToDetails(covers));
        });
    }
}
