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
                .Select(a => new { a.Id, a.Name, a.Slug })
                .SingleOrDefaultAsync();

            if (artist is null)
            {
                return Results.NotFound();
            }
            if (slug != artist.Slug)
            {
                return Results.Redirect($"/api/my-artists/{artist.Id}/{artist.Slug}");
            }

            var managers = await db.Artists.Where(a => a.Id == id).SelectMany(a => a.Managers).WithNamesAsync(db);
            return Results.Ok(new ManagedArtist(artist.Id, artist.Name, artist.Slug, managers, Releases: []));
        });

        // Two path segments after the id, so no Artist's slug can be mistaken for this list.
        api.MapGet("/artists/{id:guid}/managers/candidates", async (Guid id, ClaimsPrincipal caller, KleinGeldDbContext db) =>
        {
            var managed = await db.Artists.Where(a => a.Id == id).ManagedBy(caller.MemberId()).AnyAsync();
            if (!managed)
            {
                return Results.NotFound();
            }

            var managers = db.Artists.Where(a => a.Id == id).SelectMany(a => a.Managers);
            return Results.Ok(await db.Users.Where(m => !managers.Contains(m)).WithNamesAsync(db));
        });

        api.MapPost("/artists/{id:guid}/managers", (Guid id, AddManagerForm form, ClaimsPrincipal caller, KleinGeldDbContext db) =>
            ChangeManagersAsync(db, id, caller, async artist =>
            {
                var member = await db.Users.SingleOrDefaultAsync(m => m.Id == form.MemberId);
                if (member is null)
                {
                    return Results.NotFound();
                }

                // Adding someone who already manages changes nothing: two managers may pick the same Member.
                if (artist.Managers.All(m => m.Id != member.Id))
                {
                    artist.Managers.Add(member);
                }
                return Results.NoContent();
            }));

        api.MapDelete("/artists/{id:guid}/managers/{memberId:guid}", (Guid id, Guid memberId, ClaimsPrincipal caller, KleinGeldDbContext db) =>
            ChangeManagersAsync(db, id, caller, artist =>
            {
                var manager = artist.Managers.SingleOrDefault(m => m.Id == memberId);
                if (manager is null)
                {
                    return Task.FromResult(Results.NotFound());
                }
                if (artist.Managers.Count == 1)
                {
                    return Task.FromResult(Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        detail: "An Artist needs a manager, so the last manager can't Stop managing. To end it, delete the Artist instead."));
                }

                artist.Managers.Remove(manager);
                return Task.FromResult(Results.NoContent());
            }));
    }

    /// <summary>
    /// Changes an Artist's managers while holding a lock on the Artist, so that concurrent changes can't get past
    /// the rule that an Artist always has a manager. Anyone who doesn't manage the Artist, the Admin included, gets
    /// "not found". Anything else that removes a manager must take the same lock.
    /// </summary>
    private static Task<IResult> ChangeManagersAsync(
        KleinGeldDbContext db, Guid id, ClaimsPrincipal caller, Func<Artist, Task<IResult>> change) =>
        // Retries run the whole transaction again, so nothing tracked by a failed attempt may carry over.
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlAsync($"""SELECT 1 FROM "Artists" WHERE "Id" = {id} FOR UPDATE""");

            var artist = await db.Artists
                .Include(a => a.Managers)
                .Where(a => a.Id == id)
                .ManagedBy(caller.MemberId())
                .SingleOrDefaultAsync();
            if (artist is null)
            {
                return Results.NotFound();
            }

            var result = await change(artist);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return result;
        });

    /// <summary>
    /// Members as shown to managers, A–Z: by the name they gave when asking to join, as a Member has no name of their
    /// own, or else their email.
    /// </summary>
    private static async Task<List<ArtistManager>> WithNamesAsync(this IQueryable<Member> members, KleinGeldDbContext db)
    {
        var named = await members
            .Select(m => new
            {
                m.Id,
                m.Email,
                Name = db.JoinRequests
                    .Where(r => r.NormalizedEmail == m.NormalizedEmail && r.Status == JoinRequestStatus.Approved)
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => r.Name)
                    .FirstOrDefault(),
            })
            .ToListAsync();

        return named
            .Select(m => new ArtistManager(m.Id, m.Name ?? m.Email!))
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The visibility rule for Artists: the Admin and an Artist's managing Members see it. Every other Member will
    /// see it once it has a Published Release, which belongs here when Releases exist.
    /// </summary>
    private static IQueryable<Artist> VisibleTo(this IQueryable<Artist> artists, ClaimsPrincipal caller) =>
        caller.IsInRole(Roles.Admin) ? artists : artists.ManagedBy(caller.MemberId());

    public static IQueryable<Artist> ManagedBy(this IQueryable<Artist> artists, Guid memberId) =>
        artists.Where(a => a.Managers.Any(m => m.Id == memberId));

    public static Guid MemberId(this ClaimsPrincipal member) =>
        Guid.Parse(member.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
