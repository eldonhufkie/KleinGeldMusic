using System.Security.Claims;
using KleinGeld.Contracts;
using KleinGeld.Data;
using Microsoft.EntityFrameworkCore;

namespace KleinGeld.Web.Api;

internal static class ArtistEndpoints
{
    /// <summary>Endpoints for creating Artists and for the My Artists pages of the Members who manage them.</summary>
    public static void MapArtistEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/artists", async (ArtistForm form, ClaimsPrincipal caller, KleinGeldDbContext db) =>
        {
            var name = form.Name.Trim();
            var creator = await db.Users.SingleAsync(m => m.Id == caller.MemberId());
            var artist = new Artist { Name = name, Slug = Slugs.From(name), Managers = [creator] };
            db.Artists.Add(artist);
            await db.SaveChangesAsync();

            return Results.Created(
                $"/api/my-artists/{artist.Id}/{artist.Slug}",
                new ArtistSummary(artist.Id, artist.Name, artist.Slug));
        });

        // Anyone who doesn't manage the Artist gets "not found", so this can't confirm that an id exists. A wrong or
        // old slug redirects with a 302: a cached 301 could loop if the Artist were later renamed back.
        api.MapGet("/my-artists/{id:guid}/{slug?}", async (Guid id, string? slug, ClaimsPrincipal caller, KleinGeldDbContext db) =>
        {
            var artist = await db.Artists
                .Where(a => a.Id == id)
                .ManagedBy(caller.MemberId())
                .Select(a => new
                {
                    a.Id,
                    a.Name,
                    a.Slug,
                    Managers = a.Managers.Select(m => new
                    {
                        m.Id,
                        m.Email,
                        // The name the Member gave when asking to join: a Member has no name of their own.
                        Name = db.JoinRequests
                            .Where(r => r.NormalizedEmail == m.NormalizedEmail && r.Status == JoinRequestStatus.Approved)
                            .OrderByDescending(r => r.CreatedAt)
                            .Select(r => r.Name)
                            .FirstOrDefault(),
                    }).ToList(),
                })
                .SingleOrDefaultAsync();

            if (artist is null)
            {
                return Results.NotFound();
            }
            if (slug != artist.Slug)
            {
                return Results.Redirect($"/api/my-artists/{artist.Id}/{artist.Slug}");
            }

            var managers = artist.Managers
                .Select(m => new ArtistManager(m.Id, m.Name ?? m.Email!))
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return Results.Ok(new ManagedArtist(artist.Id, artist.Name, artist.Slug, managers, Releases: []));
        });
    }

    private static IQueryable<Artist> ManagedBy(this IQueryable<Artist> artists, Guid memberId) =>
        artists.Where(a => a.Managers.Any(m => m.Id == memberId));

    private static Guid MemberId(this ClaimsPrincipal member) =>
        Guid.Parse(member.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
