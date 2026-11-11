using System.Security.Claims;
using KleinGeld.Contracts;
using KleinGeld.Data;
using KleinGeld.Web.Blobs;
using Microsoft.EntityFrameworkCore;

namespace KleinGeld.Web.Api;

internal static class ReleaseEndpoints
{
    /// <summary>Endpoints for the managing Members of an Artist to start, edit and delete its Releases.</summary>
    public static void MapReleaseEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/artists/{artistId:guid}/releases", async (Guid artistId, ReleaseForm form, ClaimsPrincipal caller, KleinGeldDbContext db, CoverStore covers) =>
        {
            var artist = await db.Artists
                .Where(a => a.Id == artistId)
                .ManagedBy(caller.MemberId())
                .SingleOrDefaultAsync();
            if (artist is null)
            {
                return Results.NotFound();
            }

            var release = new Release
            {
                Artist = artist,
                Title = "",
                Slug = "",
                Type = form.Type!.Value,
            };
            release.Fill(form);
            db.Releases.Add(release);
            await db.SaveChangesAsync();

            return Results.Created($"/api/releases/{release.Id}/{release.Slug}", release.ToDetails(covers));
        });

        // A wrong or old slug redirects with a 302, like an Artist's; "not found" is decided first so a redirect
        // can't confirm that an id exists.
        api.MapGet("/releases/{id:guid}/{slug?}", async (Guid id, string? slug, ClaimsPrincipal caller, KleinGeldDbContext db, CoverStore covers) =>
        {
            var release = await db.Releases
                .Include(r => r.Artist)
                .Where(r => r.Id == id)
                .VisibleTo(caller)
                .SingleOrDefaultAsync();

            if (release is null)
            {
                return Results.NotFound();
            }
            if (slug != release.Slug)
            {
                return Results.Redirect($"/api/releases/{release.Id}/{release.Slug}");
            }

            // Other Members see the Release as it was, without a sign of its Pending Changes.
            var details = release.ToDetails(covers);
            var seesPending = await db.Releases.Where(r => r.Id == id).SeesPendingChanges(caller).AnyAsync();
            return seesPending
                ? Results.Ok(details)
                : Results.Ok(details with { PendingChanges = null, PendingChangesRejectionReason = null });
        });

        api.MapPut("/releases/{id:guid}", async (Guid id, ReleaseForm form, ClaimsPrincipal caller, KleinGeldDbContext db, CoverStore covers) =>
        {
            Release? edited = null;
            var refused = await db.WithUnsubmittedReleaseLockedAsync(id, caller, async release =>
            {
                release.Fill(form);
                await db.SaveChangesAsync();
                edited = release;
                return null;
            });
            return refused ?? Results.Ok(edited!.ToDetails(covers));
        });

        api.MapDelete("/releases/{id:guid}", async (Guid id, ClaimsPrincipal caller, KleinGeldDbContext db, CoverStore covers, BlobLinks links, PlayLinks playLinks) =>
        {
            List<string> blobNames = [];
            var wasPublished = false;
            var refused = await db.WithUnsubmittedReleaseLockedAsync(id, caller, async release =>
            {
                wasPublished = release.Stage == ReleaseStage.Published;
                blobNames = release.Tracks.SelectMany(t => t.BlobNames()).ToList();
                db.Releases.Remove(release);
                await db.SaveChangesAsync();
                return null;
            });
            if (refused is not null)
            {
                return refused;
            }

            // After the commit, so a link fetched meanwhile is either refused or signed against the policy that goes.
            if (wasPublished)
            {
                await playLinks.RevokeAllAsync();
            }

            await covers.DeleteAllAsync(id);

            foreach (var blobName in blobNames)
            {
                await links.Blob(TrackEndpoints.AudioContainer, blobName).DeleteIfExistsAsync();
            }

            return Results.NoContent();
        });
    }

    internal static IQueryable<Release> ManagedBy(this IQueryable<Release> releases, Guid memberId) =>
        releases.Where(r => r.Artist.Managers.Any(m => m.Id == memberId));

    private static void Fill(this Release release, ReleaseForm form)
    {
        release.Title = form.Title?.Trim() ?? "";
        release.Slug = Slugs.From(release.Title);
        release.Type = form.Type!.Value;
        release.ReleaseDate = form.ReleaseDate;
        release.Description = NullIfBlank(form.Description);
        release.Genre = NullIfBlank(form.Genre);
    }

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    internal static ReleaseDetails ToDetails(this Release release, CoverStore covers) => new(
        release.Id,
        release.Artist.Id,
        release.Artist.Name,
        release.Title,
        release.Slug,
        release.Type,
        release.ReleaseDate,
        release.Description,
        release.Genre,
        release.Stage,
        release.CoverId is { } coverId ? covers.LinksTo(release.Id, coverId) : null,
        release.PublishedAt,
        release.Stage == ReleaseStage.Rejected ? release.RejectionReason : null,
        release.PendingChanges,
        release.PendingChanges == ReleaseStage.Rejected ? release.PendingChangesRejectionReason : null);
}
