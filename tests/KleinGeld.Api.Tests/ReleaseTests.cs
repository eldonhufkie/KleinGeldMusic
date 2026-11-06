using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

[Collection(KleinGeldAppCollection.Name)]
public sealed class ReleaseTests(KleinGeldApp app)
{
    [Fact]
    public async Task A_managing_Member_starts_a_New_Release_and_My_Artists_lists_it_as_a_Draft()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var artist = await CreateArtistAsync(manager);

        var release = await CreateReleaseAsync(manager, artist, new ReleaseForm
        {
            Title = "Zomer in Zeeland",
            Type = ReleaseType.EP,
            ReleaseDate = new DateOnly(1999, 6, 21),
            Description = "Vijf liedjes over de kust.",
            Genre = "Folk",
        });

        Assert.Equal(7, release.Id.Version);
        Assert.Equal("zomer-in-zeeland", release.Slug);
        Assert.Equal(ReleaseStage.Draft, release.Stage);
        Assert.Equal(artist.Id, release.ArtistId);
        Assert.Equal("De Testers", release.ArtistName);
        Assert.Equal(ReleaseType.EP, release.Type);
        Assert.Equal(new DateOnly(1999, 6, 21), release.ReleaseDate);
        Assert.Equal("Vijf liedjes over de kust.", release.Description);
        Assert.Equal("Folk", release.Genre);

        var page = await manager.GetFromJsonAsync<ManagedArtist>($"/api/my-artists/{artist.Id}/{artist.Slug}");
        Assert.Equal(
            new ManagedRelease(release.Id, "Zomer in Zeeland", "zomer-in-zeeland", ReleaseType.EP, new DateOnly(1999, 6, 21), ReleaseStage.Draft),
            Assert.Single(page!.Releases));
    }

    [Fact]
    public async Task Description_and_genre_are_optional_and_the_release_date_may_be_long_past()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var artist = await CreateArtistAsync(manager);

        var release = await CreateReleaseAsync(manager, artist, new ReleaseForm
        {
            Title = "Oud Werk",
            Type = ReleaseType.Single,
            ReleaseDate = new DateOnly(1950, 1, 1),
            Description = new string('x', 2000),
            Genre = "   ",
        });

        Assert.Equal(2000, release.Description!.Length);
        Assert.Null(release.Genre);
    }

    public static TheoryData<object> InvalidForms => new()
    {
        new { Title = "Geen Type", ReleaseDate = "2026-01-01" },
        new { Title = "Fout Type", Type = "Compilation", ReleaseDate = "2026-01-01" },
        new { Title = "Fout Getal", Type = 7, ReleaseDate = "2026-01-01" },
        new { Title = "Te Lang", Type = "Album", ReleaseDate = "2026-01-01", Description = new string('x', 2001) },
    };

    [Theory]
    [MemberData(nameof(InvalidForms))]
    public async Task A_Release_with_invalid_details_is_refused_on_create_and_on_edit(object form)
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var artist = await CreateArtistAsync(manager);
        var release = await CreateReleaseAsync(manager, artist, ValidForm());

        using var created = await manager.PostAsJsonAsync($"/api/artists/{artist.Id}/releases", form);
        using var edited = await manager.PutAsJsonAsync($"/api/releases/{release.Id}", form);

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, edited.StatusCode);
    }

    [Fact]
    public async Task A_managing_Member_edits_a_Draft_and_the_old_slug_redirects_to_the_new_one()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var artist = await CreateArtistAsync(manager);
        var release = await CreateReleaseAsync(manager, artist, ValidForm());

        using var response = await manager.PutAsJsonAsync($"/api/releases/{release.Id}", new ReleaseForm
        {
            Title = "Nieuwe Titel",
            Type = ReleaseType.Album,
            ReleaseDate = new DateOnly(2025, 3, 4),
            Genre = "Jazz",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var followed = await manager.GetAsync($"/api/releases/{release.Id}/{release.Slug}");
        Assert.Equal($"/api/releases/{release.Id}/nieuwe-titel", followed.RequestMessage!.RequestUri!.AbsolutePath);
        var details = await followed.Content.ReadFromJsonAsync<ReleaseDetails>();
        Assert.Equal(
            new ReleaseDetails(release.Id, artist.Id, "De Testers", "Nieuwe Titel", "nieuwe-titel", ReleaseType.Album, new DateOnly(2025, 3, 4), null, "Jazz", ReleaseStage.Draft),
            details);
    }

    [Fact]
    public async Task The_last_edit_wins()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var artist = await CreateArtistAsync(manager);
        var release = await CreateReleaseAsync(manager, artist, ValidForm());
        var first = ValidForm();
        first.Title = "Eerste";
        var second = ValidForm();
        second.Title = "Tweede";

        using var firstEdit = await manager.PutAsJsonAsync($"/api/releases/{release.Id}", first);
        using var secondEdit = await manager.PutAsJsonAsync($"/api/releases/{release.Id}", second);

        Assert.Equal(HttpStatusCode.OK, firstEdit.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondEdit.StatusCode);
        var details = await manager.GetFromJsonAsync<ReleaseDetails>($"/api/releases/{release.Id}/tweede");
        Assert.Equal("Tweede", details!.Title);
    }

    [Fact]
    public async Task A_managing_Member_deletes_a_Release_and_it_is_gone()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var artist = await CreateArtistAsync(manager);
        var release = await CreateReleaseAsync(manager, artist, ValidForm());

        using var deleted = await manager.DeleteAsync($"/api/releases/{release.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var read = await manager.GetAsync($"/api/releases/{release.Id}/{release.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        var page = await manager.GetFromJsonAsync<ManagedArtist>($"/api/my-artists/{artist.Id}/{artist.Slug}");
        Assert.Empty(page!.Releases);
    }

    [Fact]
    public async Task A_Draft_is_not_found_for_other_Members_but_seen_by_its_managers_and_the_Admin()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var otherMember = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        using var outsider = app.CreateWebClient();
        var artist = await CreateArtistAsync(manager);
        var release = await CreateReleaseAsync(manager, artist, ValidForm());
        var link = $"/api/releases/{release.Id}/{release.Slug}";

        using var forOtherMember = await otherMember.GetAsync(link);
        using var wrongSlugForOtherMember = await otherMember.GetAsync($"/api/releases/{release.Id}/guess");
        using var forOutsider = await outsider.GetAsync(link);

        Assert.Equal(HttpStatusCode.NotFound, forOtherMember.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, wrongSlugForOtherMember.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, forOutsider.StatusCode);
        Assert.Equal(release, await manager.GetFromJsonAsync<ReleaseDetails>(link));
        Assert.Equal(release, await admin.GetFromJsonAsync<ReleaseDetails>(link));
    }

    [Fact]
    public async Task Only_managing_Members_create_edit_or_delete_Releases()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var otherMember = await app.LoginAsNewMemberAsync();
        var artist = await CreateArtistAsync(manager);
        var release = await CreateReleaseAsync(manager, artist, ValidForm());

        using var created = await otherMember.PostAsJsonAsync($"/api/artists/{artist.Id}/releases", ValidForm());
        using var edited = await otherMember.PutAsJsonAsync($"/api/releases/{release.Id}", ValidForm("Van Mij Nu"));
        using var deleted = await otherMember.DeleteAsync($"/api/releases/{release.Id}");

        Assert.Equal(HttpStatusCode.NotFound, created.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, edited.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
        var page = await manager.GetFromJsonAsync<ManagedArtist>($"/api/my-artists/{artist.Id}/{artist.Slug}");
        Assert.Equal(release.Title, Assert.Single(page!.Releases).Title);
    }

    private static ReleaseForm ValidForm(string title = "Eerste Plaat") => new()
    {
        Title = title,
        Type = ReleaseType.Album,
        ReleaseDate = new DateOnly(2026, 1, 1),
    };

    private static async Task<ArtistSummary> CreateArtistAsync(HttpClient member)
    {
        using var response = await member.PostAsJsonAsync("/api/artists", new ArtistForm { Name = "De Testers" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ArtistSummary>())!;
    }

    private static async Task<ReleaseDetails> CreateReleaseAsync(HttpClient member, ArtistSummary artist, ReleaseForm form)
    {
        using var response = await member.PostAsJsonAsync($"/api/artists/{artist.Id}/releases", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ReleaseDetails>())!;
    }
}
