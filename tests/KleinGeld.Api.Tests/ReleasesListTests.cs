using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

[Collection(KleinGeldAppCollection.Name)]
public sealed class ReleasesListTests(KleinGeldApp app)
{
    private const int PageSize = 48;
    private static readonly DateTimeOffset LongAgo = new(2001, 2, 3, 4, 5, 6, TimeSpan.Zero);

    [Fact]
    public async Task Newest_is_the_default_and_sorts_by_Published_date_not_release_date()
    {
        using var member = await app.LoginAsNewMemberAsync();
        var artist = await CreateArtistAsync(member);
        var tag = Guid.NewGuid().ToString("N");
        await SeedAsync(artist, ReleaseType.Album,
            new($"Middle {tag}", LongAgo.AddDays(2), new DateOnly(2020, 1, 1)),
            new($"Oldest {tag}", LongAgo.AddDays(1), new DateOnly(2025, 1, 1)),
            new($"Latest {tag}", LongAgo.AddDays(3), new DateOnly(2015, 1, 1)));

        var titles = (await ReadAllAsync(member, "/api/releases")).Select(r => r.Title).Where(t => t.EndsWith(tag));

        Assert.Equal([$"Latest {tag}", $"Middle {tag}", $"Oldest {tag}"], titles);
    }

    [Fact]
    public async Task A_to_Z_sorts_by_title_whatever_the_case()
    {
        using var member = await app.LoginAsNewMemberAsync();
        var artist = await CreateArtistAsync(member);
        var tag = Guid.NewGuid().ToString("N");
        await SeedAsync(artist, ReleaseType.EP,
            new($"Zebra {tag}", LongAgo),
            new($"aardvark {tag}", LongAgo),
            new($"Mol {tag}", LongAgo));

        var titles = (await ReadAllAsync(member, "/api/releases?sort=AZ")).Select(r => r.Title).Where(t => t.EndsWith(tag));

        Assert.Equal([$"aardvark {tag}", $"Mol {tag}", $"Zebra {tag}"], titles);
    }

    [Theory]
    [InlineData(ReleaseType.Album)]
    [InlineData(ReleaseType.EP)]
    [InlineData(ReleaseType.Single)]
    public async Task A_type_filter_shows_only_that_type(ReleaseType type)
    {
        using var member = await app.LoginAsNewMemberAsync();
        var artist = await CreateArtistAsync(member);
        var tag = Guid.NewGuid().ToString("N");
        foreach (var seeded in Enum.GetValues<ReleaseType>())
        {
            await SeedAsync(artist, seeded, new SeededRelease($"{seeded} {tag}", LongAgo));
        }

        var filtered = await ReadAllAsync(member, $"/api/releases?type={type}");
        var all = await ReadAllAsync(member, "/api/releases");

        Assert.All(filtered, r => Assert.Equal(type, r.Type));
        Assert.Equal([$"{type} {tag}"], filtered.Select(r => r.Title).Where(t => t.EndsWith(tag)));
        Assert.Equal(3, all.Count(r => r.Title.EndsWith(tag)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("&sort=AZ")]
    public async Task A_page_that_ends_the_list_exactly_has_no_next_and_one_more_Release_starts_a_new_page(string sort)
    {
        using var member = await app.LoginAsNewMemberAsync();
        var artist = await CreateArtistAsync(member);
        var url = $"/api/releases?type=Single{sort}";
        var existing = (await ReadPagesAsync(member, url)).Sum(p => p.Items.Count);
        await SeedManyAsync(artist, PageSize * 2 - existing % PageSize);

        var full = await ReadPagesAsync(member, url);

        Assert.All(full, p => Assert.Equal(PageSize, p.Items.Count));
        Assert.Null(full[^1].Next);

        await SeedManyAsync(artist, 1);

        var oneOver = await ReadPagesAsync(member, url);

        Assert.Equal(full.Count + 1, oneOver.Count);
        Assert.Single(oneOver[^1].Items);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?sort=AZ")]
    public async Task Paging_through_Releases_that_tie_skips_and_repeats_nothing(string sort)
    {
        using var member = await app.LoginAsNewMemberAsync();
        var artist = await CreateArtistAsync(member);
        var title = $"Gelijk {Guid.NewGuid():N}";
        var seeded = Enumerable.Range(0, PageSize + 12).Select(_ => new SeededRelease(title, LongAgo)).ToArray();
        await SeedAsync(artist, ReleaseType.Album, seeded);

        var listed = await ReadAllAsync(member, $"/api/releases{sort}");

        Assert.Equal(listed.Count, listed.Select(r => r.Id).Distinct().Count());
        Assert.Equal(seeded.Length, listed.Count(r => r.Title == title));
    }

    [Theory]
    [InlineData("not a cursor")]
    [InlineData("e30")]
    public async Task A_broken_paging_position_is_refused(string after)
    {
        using var member = await app.LoginAsNewMemberAsync();

        using var response = await member.GetAsync($"/api/releases?after={Uri.EscapeDataString(after)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_paging_position_from_the_other_sort_is_refused()
    {
        using var member = await app.LoginAsNewMemberAsync();
        var artist = await CreateArtistAsync(member);
        await SeedManyAsync(artist, PageSize + 1);
        var first = await member.GetFromJsonAsync<ReleaseList>("/api/releases");

        using var response = await member.GetAsync($"/api/releases?sort=AZ&after={first!.Next}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task New_Releases_are_the_12_most_recently_Published()
    {
        using var member = await app.LoginAsNewMemberAsync();
        var artist = await CreateArtistAsync(member);
        var tag = Guid.NewGuid().ToString("N");
        var start = DateTimeOffset.UtcNow.AddYears(100);
        var seeded = Enumerable.Range(0, 13)
            .Select(i => new SeededRelease($"{i} {tag}", start.AddMinutes(i), new DateOnly(2020, 1, 1).AddDays(-i)))
            .ToArray();
        await SeedAsync(artist, ReleaseType.Album, seeded);

        var home = await member.GetFromJsonAsync<List<ReleaseCard>>("/api/releases/new");

        Assert.Equal(seeded.Skip(1).Reverse().Select(r => r.Title), home!.Select(r => r.Title));
    }

    [Fact]
    public async Task Someone_not_logged_in_gets_neither_list()
    {
        using var outsider = app.CreateWebClient();

        using var grid = await outsider.GetAsync("/api/releases");
        using var home = await outsider.GetAsync("/api/releases/new");

        Assert.Equal(HttpStatusCode.Unauthorized, grid.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, home.StatusCode);
    }

    private static async Task<ArtistSummary> CreateArtistAsync(HttpClient member)
    {
        using var response = await member.PostAsJsonAsync("/api/artists", new ArtistForm { Name = "De Lijst" });
        return (await response.Content.ReadFromJsonAsync<ArtistSummary>())!;
    }

    private Task SeedManyAsync(ArtistSummary artist, int count) =>
        SeedAsync(artist, ReleaseType.Single, Enumerable.Range(0, count)
            .Select(i => new SeededRelease($"Veel {Guid.NewGuid():N}", LongAgo.AddSeconds(i)))
            .ToArray());

    private async Task SeedAsync(ArtistSummary artist, ReleaseType type, params SeededRelease[] releases)
    {
        using var client = app.CreateWebClient();
        using var response = await client.PostAsJsonAsync("/testing/published-releases", new { ArtistId = artist.Id, Type = type, Releases = releases });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<List<ReleaseCard>> ReadAllAsync(HttpClient member, string url) =>
        (await ReadPagesAsync(member, url)).SelectMany(p => p.Items).ToList();

    private static async Task<List<ReleaseList>> ReadPagesAsync(HttpClient member, string url)
    {
        var separator = url.Contains('?') ? '&' : '?';
        var pages = new List<ReleaseList> { (await member.GetFromJsonAsync<ReleaseList>(url))! };
        while (pages[^1].Next is { } next)
        {
            pages.Add((await member.GetFromJsonAsync<ReleaseList>($"{url}{separator}after={next}"))!);
        }
        return pages;
    }

    private sealed record SeededRelease(string Title, DateTimeOffset PublishedAt, DateOnly? ReleaseDate = null);
}
