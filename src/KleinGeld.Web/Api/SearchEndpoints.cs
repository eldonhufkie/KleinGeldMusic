using KleinGeld.Contracts;
using KleinGeld.Data;
using KleinGeld.Web.Blobs;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KleinGeld.Web.Api;

/// <summary>
/// One search box over Artist names, Release titles, and Track titles plus featured Artists. Only Published content
/// is found, whoever asks: like the grids, search is a Member's view. Case and accents never matter
/// (<c>search_text</c>, see the Search migration). Two characters match the start of a word; from three, trigram
/// similarity also forgives a small typo, best match first.
/// </summary>
internal static class SearchEndpoints
{
    /// <summary>How many of each group one search shows before Show all.</summary>
    private const int TopSize = 5;

    /// <summary>How many Show all loads at a time.</summary>
    private const int LoadSize = 48;

    private const int MinimumLength = 2;

    public static void MapSearchEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/search", async (string? q, KleinGeldDbContext db, CoverStore covers) =>
        {
            var query = Query.From(q, skip: 0, TopSize);
            return Results.Ok(new SearchResults(
                await ArtistsAsync(db, covers, query),
                await ReleasesAsync(db, covers, query),
                await TracksAsync(db, covers, query)));
        });

        api.MapGet("/search/artists", async (string? q, string? after, KleinGeldDbContext db, CoverStore covers) =>
            Query.From(q, after, LoadSize) is { } query ? Results.Ok(await ArtistsAsync(db, covers, query)) : Results.BadRequest());

        api.MapGet("/search/releases", async (string? q, string? after, KleinGeldDbContext db, CoverStore covers) =>
            Query.From(q, after, LoadSize) is { } query ? Results.Ok(await ReleasesAsync(db, covers, query)) : Results.BadRequest());

        api.MapGet("/search/tracks", async (string? q, string? after, KleinGeldDbContext db, CoverStore covers) =>
            Query.From(q, after, LoadSize) is { } query ? Results.Ok(await TracksAsync(db, covers, query)) : Results.BadRequest());
    }

    private static async Task<SearchPage<ArtistCard>> ArtistsAsync(KleinGeldDbContext db, CoverStore covers, Query query)
    {
        var hits = await query.HitsAsync(db, $"""
            SELECT a."Id", {query.Score("""a."Name" """)} AS "Score", search_text(a."Name") AS "Sort"
            FROM "Artists" a, term
            WHERE {query.Matches("""a."Name" """)}
              AND EXISTS (SELECT 1 FROM "Releases" r WHERE r."ArtistId" = a."Id" AND r."Stage" = 'Published')
            """);
        var found = await db.Artists
            .Where(a => hits.Contains(a.Id))
            .Select(a => new
            {
                a.Id,
                a.Name,
                a.Slug,
                Newest = a.Releases.AsQueryable()
                    .Where(Visibility.IsPublished)
                    .OrderByDescending(r => r.ReleaseDate)
                    .ThenByDescending(r => r.Id)
                    .Select(r => new { r.Id, r.CoverId })
                    .First(),
            })
            .ToDictionaryAsync(a => a.Id);
        return query.Page(hits, id => found[id], a => new ArtistCard(a.Id, a.Name, a.Slug, a.Newest.CoverId is { } cover ? covers.LinksTo(a.Newest.Id, cover) : null));
    }

    private static async Task<SearchPage<SearchRelease>> ReleasesAsync(KleinGeldDbContext db, CoverStore covers, Query query)
    {
        var hits = await query.HitsAsync(db, $"""
            SELECT r."Id", {query.Score("""r."Title" """)} AS "Score", search_text(r."Title") AS "Sort"
            FROM "Releases" r, term
            WHERE {query.Matches("""r."Title" """)} AND r."Stage" = 'Published'
            """);
        var found = await db.Releases
            .Where(r => hits.Contains(r.Id))
            .Select(r => new { r.Id, r.Title, r.Slug, r.Type, ArtistName = r.Artist.Name, r.CoverId })
            .ToDictionaryAsync(r => r.Id);
        return query.Page(hits, id => found[id], r => new SearchRelease(r.Id, r.Title, r.Slug, r.Type, r.ArtistName, r.CoverId is { } cover ? covers.LinksTo(r.Id, cover) : null));
    }

    private static async Task<SearchPage<SearchTrack>> TracksAsync(KleinGeldDbContext db, CoverStore covers, Query query)
    {
        var hits = await query.HitsAsync(db, $"""
            SELECT t."Id",
                   GREATEST({query.Score("""t."Title" """)}, COALESCE({query.Score("""t."FeaturedArtists" """)}, 0)) AS "Score",
                   search_text(t."Title") AS "Sort"
            FROM "Tracks" t JOIN "Releases" r ON r."Id" = t."ReleaseId", term
            WHERE ({query.Matches("""t."Title" """)} OR {query.Matches("""t."FeaturedArtists" """)})
              AND r."Stage" = 'Published'
            """);
        var found = await db.Tracks
            .Where(t => hits.Contains(t.Id))
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.FeaturedArtists,
                t.ReleaseId,
                ReleaseTitle = t.Release.Title,
                ReleaseSlug = t.Release.Slug,
                ArtistName = t.Release.Artist.Name,
                t.Release.CoverId,
            })
            .ToDictionaryAsync(t => t.Id);
        return query.Page(hits, id => found[id], t => new SearchTrack(
                t.Id, t.Title, t.FeaturedArtists, t.ReleaseId, t.ReleaseTitle, t.ReleaseSlug, t.ArtistName,
                t.CoverId is { } cover ? covers.LinksTo(t.ReleaseId, cover) : null));
    }

    /// <summary>A search as typed, with how much of the group's results to read.</summary>
    private sealed class Query
    {
        private readonly string _text;
        private readonly int _skip;
        private readonly int _take;

        private Query(string text, int skip, int take) => (_text, _skip, _take) = (text, skip, take);

        /// <summary>Prefix only below three characters: a trigram needs three.</summary>
        private bool IsPrefix => _text.Length < 3;

        public bool IsEmpty => _text.Length < MinimumLength;

        public static Query From(string? q, int skip, int take) => new(q?.Trim() ?? "", skip, take);

        /// <summary>Null when <paramref name="after"/> is no position this API gave out.</summary>
        public static Query? From(string? q, string? after, int take)
        {
            if (after is null)
            {
                return From(q, 0, take);
            }
            return PagingPosition.Parse<SearchPosition>(after) is { Skip: > 0 } position ? From(q, position.Skip, take) : null;
        }

        /// <summary>
        /// SQL that is true when the column matches. <c>term</c> holds the folded search and its LIKE-escaped form.
        /// </summary>
        public string Matches(string column) => IsPrefix
            ? $"""(search_text({column}) LIKE term.escaped || '%' OR search_text({column}) LIKE '% ' || term.escaped || '%')"""
            : $"""(term.text <% search_text({column}) OR search_text({column}) LIKE '%' || term.escaped || '%')""";

        public string Score(string column) => IsPrefix ? "1.0" : $"word_similarity(term.text, search_text({column}))";

        /// <summary>The ids of one more than a page of matches, best first, so we know whether there's more.</summary>
        public async Task<List<Guid>> HitsAsync(KleinGeldDbContext db, string matches)
        {
            if (IsEmpty)
            {
                return [];
            }

            var sql = $"""
                WITH term AS (
                    SELECT folded AS text, replace(replace(replace(folded, '\', '\\'), '%', '\%'), '_', '\_') AS escaped
                    FROM (SELECT search_text(@q) AS folded) f
                )
                SELECT "Id" AS "Value" FROM ({matches}) hits
                ORDER BY "Score" DESC, "Sort", "Id"
                OFFSET @skip LIMIT @take
                """;
            return await db.Database
                .SqlQueryRaw<Guid>(sql, new NpgsqlParameter("q", _text), new NpgsqlParameter("skip", _skip), new NpgsqlParameter("take", _take + 1))
                .ToListAsync();
        }

        public SearchPage<T> Page<TFound, T>(List<Guid> hits, Func<Guid, TFound> find, Func<TFound, T> toResult)
        {
            var items = hits.Take(_take).Select(find).Select(toResult).ToList();
            var next = hits.Count > _take ? PagingPosition.Format(new SearchPosition(_skip + _take)) : null;
            return new SearchPage<T>(items, next);
        }
    }

    private sealed record SearchPosition(int Skip);
}
