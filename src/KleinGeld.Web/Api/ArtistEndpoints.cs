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

        api.MapPut("/artists/{id:guid}", async (Guid id, ArtistForm form, ClaimsPrincipal caller, KleinGeldDbContext db) =>
        {
            var artist = await db.Artists
                .Where(a => a.Id == id)
                .ManagedBy(caller.MemberId())
                .SingleOrDefaultAsync();
            if (artist is null)
            {
                return Results.NotFound();
            }

            artist.Name = form.Name.Trim();
            artist.Slug = Slugs.From(artist.Name);
            await db.SaveChangesAsync();

            return Results.Ok(new ArtistSummary(artist.Id, artist.Name, artist.Slug));
        });

        api.MapGet("/artists/{id:guid}/{slug?}", async (Guid id, string? slug, ClaimsPrincipal caller, KleinGeldDbContext db) =>
        {
            var artist = await db.Artists
                .Where(a => a.Id == id)
                .VisibleTo(caller)
                .Select(a => new ArtistSummary(a.Id, a.Name, a.Slug))
                .SingleOrDefaultAsync();

            if (artist is null)
            {
                return Results.NotFound();
            }
            return slug == artist.Slug
                ? Results.Ok(artist)
                : Results.Redirect($"/api/artists/{artist.Id}/{artist.Slug}");
        });

        api.MapGet("/my-artists", async (ClaimsPrincipal caller, KleinGeldDbContext db) =>
            await db.Artists
                .ManagedBy(caller.MemberId())
                .OrderBy(a => a.Name)
                .Select(a => new ArtistSummary(a.Id, a.Name, a.Slug))
                .ToListAsync());

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
                    Releases = a.Releases
                        .OrderByDescending(r => r.ReleaseDate)
                        .ThenByDescending(r => r.Id)
                        .Select(r => new ManagedRelease(r.Id, r.Title, r.Slug, r.Type, r.ReleaseDate, r.Stage))
                        .ToList(),
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
            return Results.Ok(new ManagedArtist(artist.Id, artist.Name, artist.Slug, managers, artist.Releases));
        });
    }

    public static IQueryable<Artist> ManagedBy(this IQueryable<Artist> artists, Guid memberId) =>
        artists.Where(a => a.Managers.Any(m => m.Id == memberId));

    public static Guid MemberId(this ClaimsPrincipal member) =>
        Guid.Parse(member.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
