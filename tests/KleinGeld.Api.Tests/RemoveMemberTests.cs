using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

public sealed partial class SubmitReleaseTests
{
    [Fact]
    public async Task The_Admin_sees_every_Member_with_the_same_preview_as_Leave()
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        using var member = await app.LoginAsNewMemberAsync(email: email);
        var solo = await CreatePublishedAsync(member, "Voorproef");
        using var _ = await member.PostAsJsonAsync("/api/playlists", new PlaylistForm { Name = "Lijstje" });
        using var admin = await app.LoginAsAdminAsync();

        var id = await FindMemberIdAsync(admin, email);
        var preview = await admin.GetFromJsonAsync<DeletionPreview>($"/api/admin/members/{id}/remove");

        Assert.Equal(["Lijstje"], preview!.Playlists);
        var artist = Assert.Single(preview.Artists);
        Assert.Equal(solo.ArtistId, artist.Id);
        Assert.Equal(["Voorproef"], artist.Releases);
    }

    [Fact]
    public async Task Removing_needs_a_reason_the_Admin_role_and_not_the_Admin()
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        using var member = await app.LoginAsNewMemberAsync(email: email);
        using var admin = await app.LoginAsAdminAsync();
        var id = await FindMemberIdAsync(admin, email);
        var adminId = await FindMemberIdAsync(admin, KleinGeldApp.AdminEmail);

        using var noReason = await RemoveAsync(admin, id, "  ");
        using var byMember = await RemoveAsync(member, id, "Reden.");
        using var theAdmin = await RemoveAsync(admin, adminId, "Reden.");

        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byMember.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, theAdmin.StatusCode);
        using var stillHere = await member.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, stillHere.StatusCode);
    }

    [Fact]
    public async Task Removing_deletes_the_Member_cuts_them_off_and_sends_email_10_but_not_email_9()
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        using var member = await app.LoginAsNewMemberAsync(email: email);
        var artistName = $"Verdwenen {Guid.NewGuid():N}";
        using var createdArtist = await member.PostAsJsonAsync("/api/artists", new ArtistForm { Name = artistName });
        var solo = await CreatePublishedAsync(member, "Laatste Plaat");
        var track = (await ReadTracksAsync(member, solo)).Single();
        var link = await member.GetFromJsonAsync<PlayLink>($"/api/tracks/{track.Id}/play-link");
        using var admin = await app.LoginAsAdminAsync();
        var id = await FindMemberIdAsync(admin, email);
        var soloName = (await admin.GetFromJsonAsync<DeletionPreview>($"/api/admin/members/{id}/remove"))!
            .Artists.Single(a => a.Id == solo.ArtistId).Name;

        using var removed = await RemoveAsync(admin, id, "Spam uploads.");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        using var me = await member.GetAsync("/api/me");
        using var playLink = await member.GetAsync($"/api/tracks/{track.Id}/play-link");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, playLink.StatusCode);
        using var revoked = await BlobStorage.GetAsync(link!.Url);
        Assert.NotEqual(HttpStatusCode.OK, revoked.StatusCode);
        Assert.Empty(await StoredBlobsAsync(solo, track));
        var mailPit = app.CreateMailPit();
        var sent = await mailPit.WaitForEmailAsync(email, "removed");
        Assert.Contains("Spam uploads.", sent.Text);
        Assert.Contains(artistName, sent.Text);
        Assert.Contains(soloName, sent.Text);
        Assert.Empty(await mailPit.SearchAsync(email, "The Admin deleted"));
        var members = await admin.GetFromJsonAsync<List<MemberListing>>("/api/admin/members");
        Assert.DoesNotContain(members!, m => m.Email == email);
    }

    [Fact]
    public async Task Someone_Removed_can_ask_to_join_again_with_the_same_email()
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        using var member = await app.LoginAsNewMemberAsync(email: email);
        using var admin = await app.LoginAsAdminAsync();
        using var removed = await RemoveAsync(admin, await FindMemberIdAsync(admin, email), "Tot ziens.");
        removed.EnsureSuccessStatusCode();

        using var outsider = app.CreateWebClient();
        using var again = await outsider.PostAsJsonAsync("/api/join-requests", new JoinRequestForm
        {
            Name = "Terug",
            Email = email,
            Message = "May I come back?",
        });

        Assert.True(again.IsSuccessStatusCode, again.StatusCode.ToString());
        var open = await admin.GetFromJsonAsync<List<OpenJoinRequest>>("/api/admin/join-requests");
        Assert.Contains(open!, r => r.Email == email);
    }

    private static Task<HttpResponseMessage> RemoveAsync(HttpClient client, Guid memberId, string reason) =>
        client.PostAsJsonAsync($"/api/admin/members/{memberId}/remove", new RemoveForm { Reason = reason });

    private static async Task<Guid> FindMemberIdAsync(HttpClient admin, string email)
    {
        var members = await admin.GetFromJsonAsync<List<MemberListing>>("/api/admin/members");
        return Assert.Single(members!, m => m.Email == email).Id;
    }
}
