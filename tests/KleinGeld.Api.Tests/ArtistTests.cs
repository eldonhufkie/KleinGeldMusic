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

    [Fact]
    public async Task After_a_rename_an_outdated_slug_redirects_to_the_current_one()
    {
        using var member = await app.LoginAsNewMemberAsync();
        var artist = await CreateArtistAsync(member, "Oude Naam");

        using var renamed = await member.PutAsJsonAsync($"/api/artists/{artist.Id}", new ArtistForm { Name = "Nieuwe Naam" });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);

        using var followed = await member.GetAsync($"/api/my-artists/{artist.Id}/oude-naam");
        Assert.Equal($"/api/my-artists/{artist.Id}/nieuwe-naam", followed.RequestMessage!.RequestUri!.AbsolutePath);
        var page = await followed.Content.ReadFromJsonAsync<ManagedArtist>();
        Assert.Equal(("Nieuwe Naam", "nieuwe-naam"), (page!.Name, page.Slug));

        using var followedPublic = await member.GetAsync($"/api/artists/{artist.Id}/oude-naam");
        Assert.Equal($"/api/artists/{artist.Id}/nieuwe-naam", followedPublic.RequestMessage!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Only_a_managing_Member_renames_an_Artist()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var otherMember = await app.LoginAsNewMemberAsync();
        var artist = await CreateArtistAsync(manager, "Van Mij");

        using var response = await otherMember.PutAsJsonAsync($"/api/artists/{artist.Id}", new ArtistForm { Name = "Van Mij Nu" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var page = await manager.GetFromJsonAsync<ManagedArtist>($"/api/my-artists/{artist.Id}/{artist.Slug}");
        Assert.Equal("Van Mij", page!.Name);
    }

    [Fact]
    public async Task My_Artists_lists_only_the_Artists_a_Member_manages_and_shows_once_they_manage_one()
    {
        using var member = await app.LoginAsNewMemberAsync();
        using var otherMember = await app.LoginAsNewMemberAsync();
        await CreateArtistAsync(otherMember, "Iemand Anders");

        var before = await member.GetFromJsonAsync<MeResponse>("/api/me");
        var zebra = await CreateArtistAsync(member, "Zebra");
        var appel = await CreateArtistAsync(member, "Appel");
        var after = await member.GetFromJsonAsync<MeResponse>("/api/me");

        Assert.False(before!.ManagesArtist);
        Assert.True(after!.ManagesArtist);
        Assert.Equal([appel, zebra], await member.GetFromJsonAsync<List<ArtistSummary>>("/api/my-artists"));
    }

    [Fact]
    public async Task The_sidebar_on_the_account_pages_shows_My_Artists_only_to_managing_Members()
    {
        using var member = await app.LoginAsNewMemberAsync();

        var before = await member.GetStringAsync("/Account/Manage");
        await CreateArtistAsync(member, "Op De Kaart");
        var after = await member.GetStringAsync("/Account/Manage");

        Assert.DoesNotContain("My Artists", before);
        Assert.Contains("My Artists", after);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_Artist_needs_a_name(string name)
    {
        using var member = await app.LoginAsNewMemberAsync();

        using var response = await member.PostAsJsonAsync("/api/artists", new ArtistForm { Name = name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<ArtistSummary> CreateArtistAsync(HttpClient member, string name)
    {
        using var response = await member.PostAsJsonAsync("/api/artists", new ArtistForm { Name = name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ArtistSummary>())!;
    }
}
