using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

[Collection(KleinGeldAppCollection.Name)]
public sealed class SearchTests(KleinGeldApp app)
{
    [Fact]
    public async Task Search_finds_an_Artist_whatever_the_case_and_accents()
    {
        using var member = await app.LoginAsNewMemberAsync();
        var word = UniqueWord();
        await app.SeedSearchCatalogAsync(new($"Éé{word}", $"Plaat {word}", $"Nummer {word}"));

        var results = await SearchAsync(member, $"EE{word.ToUpperInvariant()}");

        Assert.Contains(results.Artists.Items, a => a.Name == $"Éé{word}");
    }

    [Fact]
    public async Task Search_finds_Releases_and_Tracks_by_title_and_Tracks_by_featured_Artist()
    {
        using var member = await app.LoginAsNewMemberAsync();
        var release = UniqueWord();
        var track = UniqueWord();
        var featured = UniqueWord();
        await app.SeedSearchCatalogAsync(new($"Artiest {UniqueWord()}", $"Plaat {release}", $"Nummer {track}", $"Gast {featured}"));

        Assert.Contains((await SearchAsync(member, release)).Releases.Items, r => r.Title == $"Plaat {release}");
        Assert.Contains((await SearchAsync(member, track)).Tracks.Items, t => t.Title == $"Nummer {track}");
        Assert.Contains((await SearchAsync(member, featured)).Tracks.Items, t => t.Title == $"Nummer {track}");
    }

    [Fact]
    public async Task Search_forgives_a_small_typo()
    {
        using var member = await app.LoginAsNewMemberAsync();
        var word = UniqueWord();
        await app.SeedSearchCatalogAsync(new($"Bloemenwinkel{word}", $"Plaat {UniqueWord()}", $"Nummer {UniqueWord()}"));

        var results = await SearchAsync(member, $"Bloemewinkel{word}");

        Assert.Contains(results.Artists.Items, a => a.Name == $"Bloemenwinkel{word}");
    }

    [Fact]
    public async Task Two_characters_match_the_start_of_a_word()
    {
        using var member = await app.LoginAsNewMemberAsync();
        var name = $"Qx{UniqueWord()}";
        await app.SeedSearchCatalogAsync(new($"De {name}", $"Plaat {UniqueWord()}", $"Nummer {UniqueWord()}"));

        var artists = await ShowAllArtistsAsync(member, "qx");

        Assert.Contains(artists, a => a.Name == $"De {name}");
        Assert.All(artists, a => Assert.Matches(@"(^|\s)[Qq][Xx]", a.Name));
    }

    [Fact]
    public async Task Content_that_is_not_Published_is_never_found_even_by_its_manager_or_the_Admin()
    {
        using var admin = await app.LoginAsAdminAsync();
        var word = UniqueWord();
        await app.SeedSearchCatalogAsync(new($"Artiest {word}", $"Plaat {word}", $"Nummer {word}", $"Gast {word}", Published: false));

        var results = await SearchAsync(admin, word);

        Assert.Empty(results.Artists.Items);
        Assert.Empty(results.Releases.Items);
        Assert.Empty(results.Tracks.Items);
    }

    [Fact]
    public async Task Each_group_shows_its_top_5_and_Show_all_loads_more()
    {
        using var member = await app.LoginAsNewMemberAsync();
        var word = UniqueWord();
        for (var i = 0; i < 6; i++)
        {
            await app.SeedSearchCatalogAsync(new($"Artiest {word} {i}", $"Plaat {UniqueWord()}", $"Nummer {UniqueWord()}"));
        }

        var results = await SearchAsync(member, word);
        var all = await ShowAllArtistsAsync(member, word);

        Assert.Equal(5, results.Artists.Items.Count);
        Assert.NotNull(results.Artists.Next);
        Assert.Equal(6, all.Count);
    }

    [Fact]
    public async Task Search_needs_a_login()
    {
        using var outsider = app.CreateWebClient();

        using var response = await outsider.GetAsync("/api/search?q=abc");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Letters only and unlikely to occur elsewhere, as every test shares one database.</summary>
    private static string UniqueWord() =>
        "w" + string.Concat(Guid.NewGuid().ToString("N")[..10].Select(c => (char)('a' + Convert.ToInt32(c.ToString(), 16))));

    private static async Task<SearchResults> SearchAsync(HttpClient member, string query) =>
        (await member.GetFromJsonAsync<SearchResults>($"/api/search?q={Uri.EscapeDataString(query)}"))!;

    private static async Task<List<ArtistCard>> ShowAllArtistsAsync(HttpClient member, string query)
    {
        List<ArtistCard> artists = [];
        string? after = null;
        do
        {
            var page = (await member.GetFromJsonAsync<SearchPage<ArtistCard>>(
                $"/api/search/artists?q={Uri.EscapeDataString(query)}" + (after is null ? "" : $"&after={after}")))!;
            artists.AddRange(page.Items);
            after = page.Next;
        }
        while (after is not null);
        return artists;
    }
}
