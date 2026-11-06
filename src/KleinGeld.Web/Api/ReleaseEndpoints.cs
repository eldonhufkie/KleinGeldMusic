using System.Security.Claims;
using KleinGeld.Contracts;
using KleinGeld.Data;
using Microsoft.EntityFrameworkCore;

namespace KleinGeld.Web.Api;

internal static class ReleaseEndpoints
{
    /// <summary>Endpoints for the managing Members of an Artist to start, edit and delete its Releases.</summary>
    public static void MapReleaseEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/artists/{artistId:guid}/releases", async (Guid artistId, ReleaseForm form, ClaimsPrincipal caller, KleinGeldDbContext db) =>
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
                ReleaseDate = form.ReleaseDate!.Value,
            };
            release.Fill(form);
            db.Releases.Add(release);
            await db.SaveChangesAsync();

            return Results.Created($"/api/releases/{release.Id}/{release.Slug}", release.ToDetails());
        });
    }

    private static void Fill(this Release release, ReleaseForm form)
    {
        release.Title = form.Title.Trim();
        release.Slug = Slugs.From(release.Title);
        release.Type = form.Type!.Value;
        release.ReleaseDate = form.ReleaseDate!.Value;
        release.Description = NullIfBlank(form.Description);
        release.Genre = NullIfBlank(form.Genre);
    }

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static ReleaseDetails ToDetails(this Release release) => new(
        release.Id,
        release.Artist.Id,
        release.Artist.Name,
        release.Title,
        release.Slug,
        release.Type,
        release.ReleaseDate,
        release.Description,
        release.Genre,
        release.Stage);
}
