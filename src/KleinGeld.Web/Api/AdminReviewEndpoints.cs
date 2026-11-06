using KleinGeld.Contracts;
using KleinGeld.Data;
using Microsoft.EntityFrameworkCore;

namespace KleinGeld.Web.Api;

internal static class AdminReviewEndpoints
{
    /// <summary>Endpoints for the Admin's Review page.</summary>
    public static void MapAdminReviewEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/review/count", (KleinGeldDbContext db) => db.AwaitingReviewCountAsync());

        api.MapGet("/review", async (KleinGeldDbContext db) =>
        {
            var releases = await db.Releases
                .Include(r => r.Artist)
                .Include(r => r.RightsDeclarations)
                .Where(r => r.Stage == ReleaseStage.Submitted)
                .ToListAsync();

            var memberIds = releases.SelectMany(r => r.RightsDeclarations).Select(d => d.MemberId).ToHashSet();
            var names = (await db.Users.Where(m => memberIds.Contains(m.Id)).WithNamesAsync(db)).ToDictionary(m => m.Id, m => m.Name);

            return releases
                .Select(r =>
                {
                    var declarations = r.RightsDeclarations
                        .OrderByDescending(d => d.MadeAt)
                        .Select(d => new MadeRightsDeclaration(
                            d.MemberId is { } id ? names.GetValueOrDefault(id) : null, d.MadeAt, d.WordingVersion))
                        .ToList();
                    return new ReviewRelease(r.Id, r.Title, r.Slug, r.Artist.Name, r.Type, declarations[0].MadeAt, declarations);
                })
                .OrderBy(r => r.SubmittedAt)
                .ToList();
        });
    }

    /// <summary>How much is waiting on the Review page, which the badge on the Admin link shows.</summary>
    public static Task<int> AwaitingReviewCountAsync(this KleinGeldDbContext db) =>
        db.Releases.CountAsync(r => r.Stage == ReleaseStage.Submitted);
}
