using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

// Part of SubmitReleaseTests for its helpers: an Artist only shows here once a Release has gone through approval.
public sealed partial class SubmitReleaseTests
{
    [Fact]
    public async Task The_Artists_grid_loads_48_at_a_time_from_A_to_Z()
    {
        using var member = await app.LoginAsNewMemberAsync();
        var tag = Guid.NewGuid().ToString("N");
        var names = Enumerable.Range(0, 49).Select(i => $"Reeks {tag} {i:00}").ToList();
        await app.SeedPublishedArtistsAsync(names);

        var first = await ReadArtistsAsync(member);
        var all = await ReadAllArtistsAsync(member);

        Assert.Equal(48, first.Artists.Count);
        Assert.NotNull(first.Next);
        Assert.Equal(all.Count, all.DistinctBy(a => a.Id).Count());
        Assert.Equal(names, all.Where(a => a.Name.Contains(tag)).Select(a => a.Name));
    }

    [Fact]
    public async Task Paging_through_Artists_that_tie_skips_and_repeats_nothing()
    {
        using var member = await app.LoginAsNewMemberAsync();
        var name = $"Gelijk {Guid.NewGuid():N}";
        await app.SeedPublishedArtistsAsync(Enumerable.Repeat(name, 60));

        var all = await ReadAllArtistsAsync(member);

        Assert.Equal(all.Count, all.DistinctBy(a => a.Id).Count());
        Assert.Equal(60, all.Count(a => a.Name == name));
    }

    [Fact]
    public async Task An_Artist_appearing_earlier_in_the_list_shifts_nothing_on_the_next_page()
    {
        using var member = await app.LoginAsNewMemberAsync();
        var tag = Guid.NewGuid().ToString("N");
        await app.SeedPublishedArtistsAsync(Enumerable.Range(0, 49).Select(i => $"Reeks {i:00} {tag}"));
        var pages = new List<ArtistList> { await ReadArtistsAsync(member) };
        while (!pages[^1].Artists.Any(a => a.Name.Contains(tag)) && pages[^1].Next is not null)
        {
            pages.Add(await ReadArtistsAsync(member, pages[^1].Next));
        }

        await app.SeedPublishedArtistsAsync([$"000 {tag}"]);
        var seen = pages.SelectMany(p => p.Artists).ToList();
        for (var after = pages[^1].Next; after is not null;)
        {
            var page = await ReadArtistsAsync(member, after);
            seen.AddRange(page.Artists);
            after = page.Next;
        }

        Assert.Equal(seen.Count, seen.DistinctBy(a => a.Id).Count());
    }

    [Theory]
    [InlineData("not a cursor")]
    [InlineData("e30")]
    public async Task A_broken_paging_position_is_refused_by_the_Artists_grid(string after)
    {
        using var member = await app.LoginAsNewMemberAsync();

        using var response = await member.GetAsync($"/api/artists?after={Uri.EscapeDataString(after)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_Artists_grid_ignores_case_and_accents_when_sorting()
    {
        using var member = await app.LoginAsNewMemberAsync();
        var tag = Guid.NewGuid().ToString("N");
        await app.SeedPublishedArtistsAsync([$"{tag} Zebra", $"{tag} Éik", $"{tag} appel"]);

        var artists = await ReadAllArtistsAsync(member);

        Assert.Equal(
            [$"{tag} appel", $"{tag} Éik", $"{tag} Zebra"],
            artists.Where(a => a.Name.StartsWith(tag)).Select(a => a.Name));
    }

    [Fact]
    public async Task The_Artists_grid_leaves_out_Artists_without_a_Published_Release_even_for_their_managers_and_the_Admin()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateCompleteDraftAsync(manager);
        using var submitted = await SubmitAsync(manager, release);

        Assert.DoesNotContain(await ReadAllArtistsAsync(manager), a => a.Id == release.ArtistId);
        Assert.DoesNotContain(await ReadAllArtistsAsync(admin), a => a.Id == release.ArtistId);

        using var approved = await ApproveAsync(admin, release);

        Assert.Contains(await ReadAllArtistsAsync(manager), a => a.Id == release.ArtistId);
    }

    [Fact]
    public async Task The_Artists_grid_needs_a_login()
    {
        using var outsider = app.CreateWebClient();

        using var response = await outsider.GetAsync("/api/artists");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_Artist_card_shows_the_cover_of_its_newest_Published_Release()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var artist = await CreateArtistAsync(manager);
        await PublishAsync(manager, admin, artist, ReleaseType.Album, new DateOnly(2020, 1, 1));
        var newest = await PublishAsync(manager, admin, artist, ReleaseType.Single, new DateOnly(2024, 1, 1));
        await PublishAsync(manager, admin, artist, ReleaseType.EP, new DateOnly(2022, 1, 1));
        var draft = await CreateReleaseAsync(manager, artist, ReleaseType.Album, new DateOnly(2030, 1, 1));
        await SetCoverAsync(manager, draft);

        var card = (await ReadAllArtistsAsync(manager)).Single(a => a.Id == artist.Id);

        Assert.Contains(newest.Id.ToString(), card.Cover!.Small.AbsolutePath);
    }

    [Fact]
    public async Task The_Artist_page_has_sections_by_Release_type_newest_release_date_first()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var otherMember = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var artist = await CreateArtistAsync(manager);
        var olderAlbum = await PublishAsync(manager, admin, artist, ReleaseType.Album, new DateOnly(2019, 5, 1));
        var newerAlbum = await PublishAsync(manager, admin, artist, ReleaseType.Album, new DateOnly(2023, 5, 1));
        var single = await PublishAsync(manager, admin, artist, ReleaseType.Single, new DateOnly(2021, 5, 1));

        var page = await ReadArtistAsync(otherMember, artist);

        Assert.Equal(artist.Name, page.Name);
        Assert.Equal([newerAlbum.Id, olderAlbum.Id], page.Albums.Select(r => r.Id));
        Assert.Empty(page.EPs);
        Assert.Equal(single.Id, Assert.Single(page.Singles).Id);
        Assert.Equal(newerAlbum.Id, page.Newest!.Id);
        Assert.NotNull(page.Albums[0].Cover);
    }

    [Fact]
    public async Task The_Artist_page_is_what_any_Member_sees_even_for_its_managers_and_the_Admin()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var artist = await CreateArtistAsync(manager);
        var published = await PublishAsync(manager, admin, artist, ReleaseType.Album, new DateOnly(2020, 1, 1));
        await CreateReleaseAsync(manager, artist, ReleaseType.Album, new DateOnly(2030, 1, 1));

        var forManager = await ReadArtistAsync(manager, artist);
        var forAdmin = await ReadArtistAsync(admin, artist);

        Assert.Equal(published.Id, Assert.Single(forManager.Albums).Id);
        Assert.Equal(published.Id, forManager.Newest!.Id);
        Assert.Equal(published.Id, Assert.Single(forAdmin.Albums).Id);
    }

    [Fact]
    public async Task A_manager_sees_an_empty_Artist_page_until_a_Release_is_Published()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var artist = await CreateArtistAsync(manager);
        await CreateReleaseAsync(manager, artist, ReleaseType.Album, new DateOnly(2030, 1, 1));

        var page = await ReadArtistAsync(manager, artist);

        Assert.Null(page.Newest);
        Assert.Empty(page.Albums);
    }

    [Fact]
    public async Task Every_Member_finds_Artists_in_the_sidebar_and_in_More()
    {
        using var member = await app.LoginAsNewMemberAsync();

        var page = await member.GetStringAsync("/Account/Manage");

        var sidebar = page[page.IndexOf("<nav class=\"sidebar\"", StringComparison.Ordinal)..];
        Assert.Contains("href=\"artists\"", sidebar[..sidebar.IndexOf("</nav>", StringComparison.Ordinal)]);
        var more = page[page.IndexOf("<details class=\"tab-more\"", StringComparison.Ordinal)..];
        Assert.Contains("href=\"artists\"", more);
    }

    private static async Task<ArtistSummary> CreateArtistAsync(HttpClient manager)
    {
        using var response = await manager.PostAsJsonAsync("/api/artists", new ArtistForm { Name = $"Artiest {Guid.NewGuid():N}" });
        return (await response.Content.ReadFromJsonAsync<ArtistSummary>())!;
    }

    private static async Task<ReleaseDetails> CreateReleaseAsync(HttpClient manager, ArtistSummary artist, ReleaseType type, DateOnly date)
    {
        using var response = await manager.PostAsJsonAsync(
            $"/api/artists/{artist.Id}/releases",
            new ReleaseForm { Title = $"{type} {date:yyyy}", Type = type, ReleaseDate = date });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ReleaseDetails>())!;
    }

    private static async Task<ReleaseDetails> PublishAsync(
        HttpClient manager, HttpClient admin, ArtistSummary artist, ReleaseType type, DateOnly date)
    {
        var release = await CreateReleaseAsync(manager, artist, type, date);
        await SetCoverAsync(manager, release);
        await WaitUntilConvertedAsync(manager, release, await AddTrackAsync(manager, release, "tone.flac"));
        using var submitted = await SubmitAsync(manager, release);
        using var approved = await ApproveAsync(admin, release);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        return release;
    }

    private static async Task<ArtistDetails> ReadArtistAsync(HttpClient member, ArtistSummary artist) =>
        (await member.GetFromJsonAsync<ArtistDetails>($"/api/artists/{artist.Id}/{artist.Slug}"))!;

    private static async Task<ArtistList> ReadArtistsAsync(HttpClient member, string? after = null) =>
        (await member.GetFromJsonAsync<ArtistList>(after is null ? "/api/artists" : $"/api/artists?after={after}"))!;

    /// <summary>Every test shares one app, so the grid may hold more than one page of other tests' Artists.</summary>
    private static async Task<List<ArtistCard>> ReadAllArtistsAsync(HttpClient member)
    {
        List<ArtistCard> artists = [];
        string? after = null;
        do
        {
            var page = await ReadArtistsAsync(member, after);
            artists.AddRange(page.Artists);
            after = page.Next;
        }
        while (after is not null);
        return artists;
    }
}
