using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

public sealed partial class SubmitReleaseTests
{
    [Fact]
    public async Task Leaving_needs_the_password_again()
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        using var member = await app.LoginAsNewMemberAsync(email: email);

        using var refused = await LeaveAsync(member, "not-my-password");
        using var withNothing = await member.PostAsJsonAsync("/api/me/leave", new LeaveForm());

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withNothing.StatusCode);
        using var stillHere = await member.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, stillHere.StatusCode);
    }

    [Fact]
    public async Task Leaving_deletes_every_Join_Request_with_the_Members_email_whatever_its_status()
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        using var member = await app.LoginAsNewMemberAsync(email: email);
        using var outsider = app.CreateWebClient();
        using var again = await outsider.PostAsJsonAsync("/api/join-requests", new JoinRequestForm
        {
            Name = "Nog Een Keer",
            Email = email,
            Message = "Asking again.",
        });
        Assert.Equal(2, await app.CountJoinRequestsAsync(email));

        using var left = await LeaveAsync(member, NewMembers.Password);

        Assert.Equal(HttpStatusCode.NoContent, left.StatusCode);
        Assert.Equal(0, await app.CountJoinRequestsAsync(email));
    }

    [Fact]
    public async Task A_passkey_that_isnt_one_is_refused_like_a_wrong_password()
    {
        using var member = await app.LoginAsNewMemberAsync();

        using var refused = await member.PostAsJsonAsync("/api/me/leave", new LeaveForm { PasskeyCredentialJson = "{\"id\":\"nope\"}" });

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        using var stillHere = await member.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, stillHere.StatusCode);
    }

    [Fact]
    public async Task The_Leave_confirmation_lists_Playlists_and_solely_managed_Artists_with_their_Releases()
    {
        using var member = await app.LoginAsNewMemberAsync();
        var bandmateName = $"Bandmate {Guid.NewGuid():N}";
        using var bandmate = await app.LoginAsNewMemberAsync(bandmateName);
        var solo = await CreatePublishedAsync(member, "Alleen Ik");
        var shared = await CreateSharedArtistAsync(member, bandmate, bandmateName, "Samen");
        using var _ = await member.PostAsJsonAsync("/api/playlists", new PlaylistForm { Name = "Op De Fiets" });

        var preview = await member.GetFromJsonAsync<DeletionPreview>("/api/me/leave");

        Assert.Equal(["Op De Fiets"], preview!.Playlists);
        var artist = Assert.Single(preview.Artists);
        Assert.Equal(solo.ArtistId, artist.Id);
        Assert.Equal(["Alleen Ik"], artist.Releases);
        Assert.DoesNotContain(preview.Artists, a => a.Id == shared);
        Assert.Contains("erased within 30 days", preview.BackupNotice);
    }

    [Fact]
    public async Task Leaving_deletes_everything_listed_and_leaves_shared_Artists_with_their_other_managers()
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        using var member = await app.LoginAsNewMemberAsync(email: email);
        var bandmateName = $"Bandmate {Guid.NewGuid():N}";
        using var bandmate = await app.LoginAsNewMemberAsync(bandmateName);
        var solo = await CreatePublishedAsync(member, "Weg Ermee");
        var track = (await ReadTracksAsync(member, solo)).Single();
        var shared = await CreateSharedArtistAsync(member, bandmate, bandmateName, "Blijft Bestaan");
        using var created = await member.PostAsJsonAsync("/api/playlists", new PlaylistForm { Name = "Weg" });
        var playlist = (await created.Content.ReadFromJsonAsync<PlaylistSummary>())!;
        var link = await bandmate.GetFromJsonAsync<PlayLink>($"/api/tracks/{track.Id}/play-link");
        var mailPit = app.CreateMailPit();
        var emailsBefore = (await mailPit.SearchAsync(email, "")).Count;

        using var left = await LeaveAsync(member, NewMembers.Password);

        Assert.Equal(HttpStatusCode.NoContent, left.StatusCode);
        Assert.Empty(await StoredBlobsAsync(solo, track));
        using var soloPage = await bandmate.GetAsync($"/api/artists/{solo.ArtistId}");
        Assert.Equal(HttpStatusCode.NotFound, soloPage.StatusCode);
        using var revoked = await BlobStorage.GetAsync(link!.Url);
        Assert.NotEqual(HttpStatusCode.OK, revoked.StatusCode);
        var sharedPage = await bandmate.GetFromJsonAsync<ManagedArtist>($"/api/my-artists/{shared}");
        Assert.Single(sharedPage!.Managers);
        using var meAfter = await member.GetAsync("/api/me");
        using var playlistAfter = await member.GetAsync($"/api/playlists/{playlist.Id}");
        Assert.NotEqual(HttpStatusCode.OK, meAfter.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, playlistAfter.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, meAfter.StatusCode);
        using var loginAgain = await app.LoginAsync(email, NewMembers.Password);
        using var meAfterLogin = await loginAgain.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meAfterLogin.StatusCode);
        Assert.Equal(0, await app.CountJoinRequestsAsync(email));
        Assert.Equal(emailsBefore, (await mailPit.SearchAsync(email, "")).Count);
    }

    [Fact]
    public async Task The_Admin_cant_Leave()
    {
        using var admin = await app.LoginAsAdminAsync();

        using var refused = await LeaveAsync(admin, KleinGeldApp.AdminPassword);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        using var stillHere = await admin.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, stillHere.StatusCode);
    }

    private static Task<HttpResponseMessage> LeaveAsync(HttpClient member, string password) =>
        member.PostAsJsonAsync("/api/me/leave", new LeaveForm { Password = password });

    /// <summary>A new Artist the member manages together with the bandmate, by its id.</summary>
    private static async Task<Guid> CreateSharedArtistAsync(HttpClient member, HttpClient bandmate, string bandmateName, string name)
    {
        using var created = await member.PostAsJsonAsync("/api/artists", new ArtistForm { Name = name });
        var artist = (await created.Content.ReadFromJsonAsync<ArtistSummary>())!;
        var candidates = await member.GetFromJsonAsync<List<ArtistManager>>($"/api/artists/{artist.Id}/managers/candidates");
        var picked = candidates!.First(c => c.Name == bandmateName);
        using var added = await member.PostAsJsonAsync($"/api/artists/{artist.Id}/managers", new AddManagerForm { MemberId = picked.Id });
        added.EnsureSuccessStatusCode();
        var page = await bandmate.GetAsync($"/api/my-artists/{artist.Id}");
        page.EnsureSuccessStatusCode();
        return artist.Id;
    }
}
