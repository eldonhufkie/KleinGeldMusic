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
            var managed = await db.Releases.Where(r => r.Id == id).ManagedBy(caller.MemberId()).AnyAsync();
            return managed ? Results.Ok(await covers.CreateUploadLinkAsync(id)) : Results.NotFound();
        });

        api.MapPost("/releases/{id:guid}/cover", async (Guid id, ConfirmUploadForm form, ClaimsPrincipal caller, KleinGeldDbContext db, CoverStore covers) =>
        {
            var release = await db.Releases
                .Include(r => r.Artist)
                .Where(r => r.Id == id)
                .ManagedBy(caller.MemberId())
                .SingleOrDefaultAsync();
            if (release is null)
            {
                return Results.NotFound();
            }

            var refusal = await covers.AcceptAsync(id, form.UploadId!.Value);
            if (refusal is not null)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, detail: refusal);
            }

            release.CoverId = form.UploadId;
            await db.SaveChangesAsync();

            return Results.Ok(release.ToDetails(covers));
        });
    }
}
