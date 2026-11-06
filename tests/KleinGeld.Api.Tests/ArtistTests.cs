using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

[Collection(KleinGeldAppCollection.Name)]
public sealed class ArtistTests(KleinGeldApp app)
{
    [Fact]
    public async Task A_Member_creates_an_Artist_and_becomes_its_managing_Member()
    {
        using var member = await app.LoginAsNewMemberAsync("Anna Bakker");

        var artist = await CreateArtistAsync(member, "De Kleine Kapél");

        Assert.Equal(7, artist.Id.Version);
        Assert.Equal("de-kleine-kapel", artist.Slug);
        var page = await member.GetFromJsonAsync<ManagedArtist>($"/api/my-artists/{artist.Id}/{artist.Slug}");
        Assert.Equal("De Kleine Kapél", page!.Name);
        Assert.Equal("Anna Bakker", Assert.Single(page.Managers).Name);
        Assert.Empty(page.Releases);
    }

    [Fact]
    public async Task An_Artist_without_a_Published_Release_is_not_found_for_other_Members()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var otherMember = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var artist = await CreateArtistAsync(manager, "Nog Niet Uit");
        var publicPage = $"/api/artists/{artist.Id}/{artist.Slug}";

        using var forOtherMember = await otherMember.GetAsync(publicPage);
        using var wrongSlugForOtherMember = await otherMember.GetAsync($"/api/artists/{artist.Id}/guess");
        using var myArtistsForOtherMember = await otherMember.GetAsync($"/api/my-artists/{artist.Id}/{artist.Slug}");

        Assert.Equal(HttpStatusCode.NotFound, forOtherMember.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, wrongSlugForOtherMember.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, myArtistsForOtherMember.StatusCode);
        Assert.Equal(artist, await manager.GetFromJsonAsync<ArtistSummary>(publicPage));
        Assert.Equal(artist, await admin.GetFromJsonAsync<ArtistSummary>(publicPage));
    }

    private static async Task<ArtistSummary> CreateArtistAsync(HttpClient member, string name)
    {
        using var response = await member.PostAsJsonAsync("/api/artists", new ArtistForm { Name = name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ArtistSummary>())!;
    }
}
