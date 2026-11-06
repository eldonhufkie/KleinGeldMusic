using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

[Collection(KleinGeldAppCollection.Name)]
public sealed class ArtistTests(KleinGeldApp app)
{
    // Unique per test: every test shares one app, so the list of Members holds everyone's Members.
    private readonly string _anna = $"Anna {Guid.NewGuid():N}";
    private readonly string _bram = $"Bram {Guid.NewGuid():N}";

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

    [Fact]
    public async Task A_managing_Member_adds_a_Member_picked_from_the_list_and_they_manage_at_once()
    {
        using var manager = await app.LoginAsNewMemberAsync(_anna);
        using var bandmate = await app.LoginAsNewMemberAsync(_bram);
        var artist = await CreateArtistAsync(manager, "De Band");

        var candidates = await manager.GetFromJsonAsync<List<ArtistManager>>($"/api/artists/{artist.Id}/managers/candidates");
        var picked = Assert.Single(candidates!, m => m.Name == _bram);
        Assert.DoesNotContain(candidates!, m => m.Name == _anna);

        using var added = await AddManagerAsync(manager, artist, picked.Id);

        Assert.Equal(HttpStatusCode.NoContent, added.StatusCode);
        var page = await bandmate.GetFromJsonAsync<ManagedArtist>($"/api/my-artists/{artist.Id}/{artist.Slug}");
        Assert.Equal([_anna, _bram], page!.Managers.Select(m => m.Name));
    }

    [Fact]
    public async Task Any_manager_makes_another_manager_Stop_managing()
    {
        using var manager = await app.LoginAsNewMemberAsync(_anna);
        using var bandmate = await app.LoginAsNewMemberAsync(_bram);
        var artist = await CreateArtistAsync(manager, "Twee Man Sterk");
        var bandmateId = await AddAsManagerAsync(manager, artist, _bram);

        // The one who was added has the same say as the one who created the Artist.
        var page = await bandmate.GetFromJsonAsync<ManagedArtist>($"/api/my-artists/{artist.Id}/{artist.Slug}");
        var creatorId = Assert.Single(page!.Managers, m => m.Name == _anna).Id;
        using var stopped = await StopManagingAsync(bandmate, artist, creatorId);

        Assert.Equal(HttpStatusCode.NoContent, stopped.StatusCode);
        using var forCreator = await manager.GetAsync($"/api/my-artists/{artist.Id}/{artist.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, forCreator.StatusCode);
        page = await bandmate.GetFromJsonAsync<ManagedArtist>($"/api/my-artists/{artist.Id}/{artist.Slug}");
        Assert.Equal(bandmateId, Assert.Single(page!.Managers).Id);
    }

    [Fact]
    public async Task A_manager_makes_themselves_Stop_managing()
    {
        using var manager = await app.LoginAsNewMemberAsync(_anna);
        using var bandmate = await app.LoginAsNewMemberAsync(_bram);
        var artist = await CreateArtistAsync(manager, "Ik Stop Ermee");
        var bandmateId = await AddAsManagerAsync(manager, artist, _bram);

        using var stopped = await StopManagingAsync(bandmate, artist, bandmateId);

        Assert.Equal(HttpStatusCode.NoContent, stopped.StatusCode);
        Assert.False((await bandmate.GetFromJsonAsync<MeResponse>("/api/me"))!.ManagesArtist);
        var page = await manager.GetFromJsonAsync<ManagedArtist>($"/api/my-artists/{artist.Id}/{artist.Slug}");
        Assert.Equal(_anna, Assert.Single(page!.Managers).Name);
    }

    [Fact]
    public async Task The_last_manager_cant_Stop_managing_and_is_told_to_delete_the_Artist()
    {
        using var manager = await app.LoginAsNewMemberAsync(_anna);
        var artist = await CreateArtistAsync(manager, "Solo");
        var page = await manager.GetFromJsonAsync<ManagedArtist>($"/api/my-artists/{artist.Id}/{artist.Slug}");

        using var refused = await StopManagingAsync(manager, artist, Assert.Single(page!.Managers).Id);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var problem = await refused.Content.ReadFromJsonAsync<Refusal>();
        Assert.Contains("delete the Artist", problem!.Detail);
        page = await manager.GetFromJsonAsync<ManagedArtist>($"/api/my-artists/{artist.Id}/{artist.Slug}");
        Assert.Single(page!.Managers);
    }

    [Fact]
    public async Task Two_managers_making_each_other_Stop_managing_at_once_leave_one_manager()
    {
        using var manager = await app.LoginAsNewMemberAsync(_anna);
        using var bandmate = await app.LoginAsNewMemberAsync(_bram);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var artist = await CreateArtistAsync(manager, $"Ruzie {attempt}");
            var bandmateId = await AddAsManagerAsync(manager, artist, _bram);
            var page = await bandmate.GetFromJsonAsync<ManagedArtist>($"/api/my-artists/{artist.Id}/{artist.Slug}");
            var managerId = Assert.Single(page!.Managers, m => m.Name == _anna).Id;

            var responses = await Task.WhenAll(
                StopManagingAsync(manager, artist, bandmateId),
                StopManagingAsync(bandmate, artist, managerId));

            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NoContent);
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task A_Member_who_doesnt_manage_the_Artist_has_no_control_over_its_managers()
    {
        using var manager = await app.LoginAsNewMemberAsync(_anna);
        using var outsider = await app.LoginAsNewMemberAsync();
        var artist = await CreateArtistAsync(manager, "Niet Van Jou");
        var bandmateId = await AddNewManagerAsync(manager, artist);
        var page = await manager.GetFromJsonAsync<ManagedArtist>($"/api/my-artists/{artist.Id}/{artist.Slug}");

        await AssertNoControlAsync(outsider, artist, bandmateId);

        var after = await manager.GetFromJsonAsync<ManagedArtist>($"/api/my-artists/{artist.Id}/{artist.Slug}");
        Assert.Equal(page!.Managers, after!.Managers);
    }

    [Fact]
    public async Task The_Admin_has_no_special_control_over_managers()
    {
        using var manager = await app.LoginAsNewMemberAsync(_anna);
        using var admin = await app.LoginAsAdminAsync();
        var artist = await CreateArtistAsync(manager, "Geen Baas");
        var bandmateId = await AddNewManagerAsync(manager, artist);
        var page = await manager.GetFromJsonAsync<ManagedArtist>($"/api/my-artists/{artist.Id}/{artist.Slug}");

        await AssertNoControlAsync(admin, artist, bandmateId);

        var after = await manager.GetFromJsonAsync<ManagedArtist>($"/api/my-artists/{artist.Id}/{artist.Slug}");
        Assert.Equal(page!.Managers, after!.Managers);
    }

    [Fact]
    public async Task Adding_someone_who_isnt_a_Member_or_stopping_someone_who_doesnt_manage_is_not_found()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var artist = await CreateArtistAsync(manager, "Onbekend");

        using var added = await AddManagerAsync(manager, artist, Guid.NewGuid());
        using var stopped = await StopManagingAsync(manager, artist, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, added.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, stopped.StatusCode);
    }

    [Fact]
    public async Task Adding_a_Member_who_already_manages_changes_nothing()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var artist = await CreateArtistAsync(manager, "Dubbelop");
        var bandmateId = await AddNewManagerAsync(manager, artist);

        using var again = await AddManagerAsync(manager, artist, bandmateId);

        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        var page = await manager.GetFromJsonAsync<ManagedArtist>($"/api/my-artists/{artist.Id}/{artist.Slug}");
        Assert.Equal(2, page!.Managers.Count);
    }

    private static async Task AssertNoControlAsync(HttpClient caller, ArtistSummary artist, Guid managerId)
    {
        using var candidates = await caller.GetAsync($"/api/artists/{artist.Id}/managers/candidates");
        using var added = await AddManagerAsync(caller, artist, managerId);
        using var stopped = await StopManagingAsync(caller, artist, managerId);

        Assert.Equal(HttpStatusCode.NotFound, candidates.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, added.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, stopped.StatusCode);
    }

    private async Task<Guid> AddNewManagerAsync(HttpClient manager, ArtistSummary artist)
    {
        var name = $"Member {Guid.NewGuid():N}";
        (await app.LoginAsNewMemberAsync(name)).Dispose();
        return await AddAsManagerAsync(manager, artist, name);
    }

    // Picked by name from the list, as a manager would.
    private static async Task<Guid> AddAsManagerAsync(HttpClient manager, ArtistSummary artist, string name)
    {
        var candidates = await manager.GetFromJsonAsync<List<ArtistManager>>($"/api/artists/{artist.Id}/managers/candidates");
        var picked = candidates!.First(m => m.Name == name);
        using var added = await AddManagerAsync(manager, artist, picked.Id);
        Assert.Equal(HttpStatusCode.NoContent, added.StatusCode);
        return picked.Id;
    }

    private static Task<HttpResponseMessage> StopManagingAsync(HttpClient member, ArtistSummary artist, Guid managerId) =>
        member.DeleteAsync($"/api/artists/{artist.Id}/managers/{managerId}");

    private sealed record Refusal(string Detail);

    private static Task<HttpResponseMessage> AddManagerAsync(HttpClient member, ArtistSummary artist, Guid memberId) =>
        member.PostAsJsonAsync($"/api/artists/{artist.Id}/managers", new AddManagerForm { MemberId = memberId });

    private static async Task<ArtistSummary> CreateArtistAsync(HttpClient member, string name)
    {
        using var response = await member.PostAsJsonAsync("/api/artists", new ArtistForm { Name = name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ArtistSummary>())!;
    }
}
