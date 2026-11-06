using System.Buffers.Text;
using System.Text.Json;
using KleinGeld.Contracts;
using KleinGeld.Data;
using KleinGeld.Web.Blobs;
using Microsoft.EntityFrameworkCore;

namespace KleinGeld.Web.Api;

internal static class ReleaseListEndpoints
{
    private const int PageSize = 48;
    private const int NewReleasesCount = 12;

    /// <summary>
    /// The Releases grid and the New Releases on the home page. Only Published Releases appear, for everyone: managers
    /// and the Admin find the rest on their own pages.
    /// </summary>
    public static void MapReleaseListEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/releases", async (ReleaseType? type, ReleaseSort? sort, string? after, KleinGeldDbContext db, CoverStore covers) =>
        {
            var position = after is null ? null : Position.Parse(after);
            var bySort = sort ?? ReleaseSort.Newest;
            if (after is not null && position?.FitsSort(bySort) != true)
            {
                return Results.BadRequest();
            }

            var releases = db.Releases.Where(r => r.Stage == ReleaseStage.Published);
            if (type is { } wanted)
            {
                releases = releases.Where(r => r.Type == wanted);
            }

            // The id settles ties, so that a position is exact and nothing is skipped or repeated between pages.
            var page = bySort == ReleaseSort.AZ
                ? releases.After(position?.Title, position?.Id).OrderBy(r => r.Title).ThenBy(r => r.Id)
                : releases.Before(position?.PublishedAt, position?.Id).OrderByDescending(r => r.PublishedAt).ThenByDescending(r => r.Id);

            // One more than a page tells whether another page follows.
            var cards = await page.Take(PageSize + 1).ToCardsAsync(covers);
            if (cards.Count <= PageSize)
            {
                return Results.Ok(new ReleaseList(cards, null));
            }

            cards.RemoveAt(PageSize);
            return Results.Ok(new ReleaseList(cards, Position.Of(cards[^1], bySort).Format()));
        });

        api.MapGet("/releases/new", async (KleinGeldDbContext db, CoverStore covers) =>
            await db.Releases
                .Where(r => r.Stage == ReleaseStage.Published)
                .OrderByDescending(r => r.PublishedAt)
                .ThenByDescending(r => r.Id)
                .Take(NewReleasesCount)
                .ToCardsAsync(covers));
    }

    private static IQueryable<Release> After(this IQueryable<Release> releases, string? title, Guid? id) =>
        title is null
            ? releases
            : releases.Where(r => EF.Functions.GreaterThan(ValueTuple.Create(r.Title, r.Id), ValueTuple.Create(title, id!.Value)));

    private static IQueryable<Release> Before(this IQueryable<Release> releases, DateTimeOffset? publishedAt, Guid? id) =>
        publishedAt is null
            ? releases
            : releases.Where(r => EF.Functions.LessThan(ValueTuple.Create(r.PublishedAt, r.Id), ValueTuple.Create(publishedAt, id!.Value)));

    private static async Task<List<ReleaseCard>> ToCardsAsync(this IQueryable<Release> releases, CoverStore covers)
    {
        var rows = await releases
            .Select(r => new { r.Id, r.Title, r.Slug, r.Type, ArtistName = r.Artist.Name, r.PublishedAt, r.CoverId })
            .ToListAsync();

        return rows
            .Select(r => new ReleaseCard(
                r.Id,
                r.Title,
                r.Slug,
                r.Type,
                r.ArtistName,
                r.PublishedAt!.Value,
                r.CoverId is { } coverId ? covers.LinksTo(r.Id, coverId).Small : null))
            .ToList();
    }

    /// <summary>Where a page ended, as the opaque <c>after</c> value that asks for the next one.</summary>
    private sealed record Position(Guid Id, DateTimeOffset? PublishedAt, string? Title)
    {
        public static Position Of(ReleaseCard last, ReleaseSort sort) => sort == ReleaseSort.AZ
            ? new Position(last.Id, null, last.Title)
            : new Position(last.Id, last.PublishedAt, null);

        public static Position? Parse(string text)
        {
            try
            {
                var position = JsonSerializer.Deserialize<Position>(Base64Url.DecodeFromChars(text));
                // PostgreSQL's driver only takes UTC, and a client may have sent anything.
                return position is { Id: var id } && id != Guid.Empty
                    ? position with { PublishedAt = position.PublishedAt?.ToUniversalTime() }
                    : null;
            }
            catch (Exception e) when (e is FormatException or JsonException)
            {
                return null;
            }
        }

        public string Format() => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(this));

        public bool FitsSort(ReleaseSort sort) => sort == ReleaseSort.AZ ? Title is not null : PublishedAt is not null;
    }
}
