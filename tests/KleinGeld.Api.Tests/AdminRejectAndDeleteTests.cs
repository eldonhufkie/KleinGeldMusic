using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

public sealed partial class SubmitReleaseTests
{
    [Fact]
    public async Task The_Admin_Rejects_a_Published_Release_with_a_reason_and_its_managers_get_email_8()
    {
        var managerEmail = $"{Guid.NewGuid():N}@example.test";
        using var manager = await app.LoginAsNewMemberAsync(email: managerEmail);
        using var otherMember = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var title = $"Toch Niet {Guid.NewGuid():N}";
        var release = await CreatePublishedAsync(manager, title);

        using var rejected = await RejectPublishedAsync(admin, release, "Not your recording.");

        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var details = await ReadAsync(manager, release);
        Assert.Equal(ReleaseStage.Rejected, details.Stage);
        Assert.Equal("Not your recording.", details.RejectionReason);
        using var hidden = await otherMember.GetAsync($"/api/releases/{release.Id}/{release.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        var sent = await app.CreateMailPit().WaitForEmailAsync(managerEmail, "was rejected");
        Assert.Contains(title, sent.Subject);
        Assert.Contains("Not your recording.", sent.Text);
    }

    [Fact]
    public async Task Rejecting_a_Published_Release_revokes_its_play_links()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var listener = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreatePublishedAsync(manager);
        var track = (await ReadTracksAsync(manager, release)).Single();
        var before = await listener.GetFromJsonAsync<PlayLink>($"/api/tracks/{track.Id}/play-link");

        using var rejected = await RejectPublishedAsync(admin, release, "Weg.");

        using var revoked = await BlobStorage.GetAsync(before!.Url);
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
    }

    [Fact]
    public async Task Rejecting_a_Published_Release_needs_a_reason_and_a_Published_Release_and_the_Admin()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var published = await CreatePublishedAsync(manager);
        var draft = await CreateCompleteDraftAsync(manager);

        using var noReason = await RejectPublishedAsync(admin, published, "  ");
        using var ofDraft = await RejectPublishedAsync(admin, draft, "Reden.");
        using var byManager = await RejectPublishedAsync(manager, published, "Reden.");

        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        await AssertRefusedAsync(ofDraft, HttpStatusCode.Conflict, "Only a Published Release");
        Assert.Equal(HttpStatusCode.Forbidden, byManager.StatusCode);
        Assert.Equal(ReleaseStage.Published, (await ReadAsync(manager, published)).Stage);
    }

    [Fact]
    public async Task The_Admin_deletes_a_Release_with_a_reason_and_its_managers_get_email_9()
    {
        var managerEmail = $"{Guid.NewGuid():N}@example.test";
        using var manager = await app.LoginAsNewMemberAsync(email: managerEmail);
        using var listener = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var title = $"Verdwenen {Guid.NewGuid():N}";
        var release = await CreatePublishedAsync(manager, title);
        var track = (await ReadTracksAsync(manager, release)).Single();
        var before = await listener.GetFromJsonAsync<PlayLink>($"/api/tracks/{track.Id}/play-link");

        using var deleted = await DeleteReleaseAsync(admin, release, "Copyright claim.");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var gone = await manager.GetAsync($"/api/releases/{release.Id}/{release.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        using var revoked = await BlobStorage.GetAsync(before!.Url);
        Assert.NotEqual(HttpStatusCode.OK, revoked.StatusCode);
        var sent = await app.CreateMailPit().WaitForEmailAsync(managerEmail, "was deleted");
        Assert.Contains(title, sent.Subject);
        Assert.Contains("Copyright claim.", sent.Text);
    }

    [Fact]
    public async Task The_Admin_deletes_an_Artist_with_a_reason_and_its_managers_get_email_9()
    {
        var managerEmail = $"{Guid.NewGuid():N}@example.test";
        using var manager = await app.LoginAsNewMemberAsync(email: managerEmail);
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreatePublishedAsync(manager);
        var artistName = (await manager.GetFromJsonAsync<ManagedArtist>($"/api/my-artists/{release.ArtistId}"))!.Name;

        using var deleted = await admin.PostAsJsonAsync(
            $"/api/admin/artists/{release.ArtistId}/delete", new DeleteForm { Reason = "Impersonation." });

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var artistGone = await manager.GetAsync($"/api/my-artists/{release.ArtistId}");
        using var releaseGone = await manager.GetAsync($"/api/releases/{release.Id}/{release.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, artistGone.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, releaseGone.StatusCode);
        var sent = await app.CreateMailPit().WaitForEmailAsync(managerEmail, "was deleted");
        Assert.Contains(artistName, sent.Subject);
        Assert.Contains("Impersonation.", sent.Text);
    }

    [Fact]
    public async Task Deleting_needs_a_reason_and_the_Admin()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreatePublishedAsync(manager);

        using var noReason = await DeleteReleaseAsync(admin, release, "");
        using var artistNoReason = await admin.PostAsJsonAsync($"/api/admin/artists/{release.ArtistId}/delete", new DeleteForm { Reason = " " });
        using var byManager = await DeleteReleaseAsync(manager, release, "Zelf.");
        using var unknown = await admin.PostAsJsonAsync($"/api/admin/artists/{Guid.NewGuid()}/delete", new DeleteForm { Reason = "Niets." });

        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, artistNoReason.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byManager.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(ReleaseStage.Published, (await ReadAsync(manager, release)).Stage);
    }

    [Fact]
    public async Task The_Admin_cannot_take_down_another_Artists_Release_nor_see_a_Members_Playlist()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreatePublishedAsync(manager);
        using var created = await manager.PostAsJsonAsync("/api/playlists", new PlaylistForm { Name = "Privé" });
        var playlist = (await created.Content.ReadFromJsonAsync<PlaylistSummary>())!;

        using var takenDown = await TakeDownAsync(admin, release);
        using var read = await admin.GetAsync($"/api/playlists/{playlist.Id}/{playlist.Slug}");

        Assert.Equal(HttpStatusCode.NotFound, takenDown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(ReleaseStage.Published, (await ReadAsync(manager, release)).Stage);
    }

    private static Task<HttpResponseMessage> RejectPublishedAsync(HttpClient admin, ReleaseDetails release, string reason) =>
        admin.PostAsJsonAsync($"/api/admin/releases/{release.Id}/reject", new RejectForm { Reason = reason });

    private static Task<HttpResponseMessage> DeleteReleaseAsync(HttpClient admin, ReleaseDetails release, string reason) =>
        admin.PostAsJsonAsync($"/api/admin/releases/{release.Id}/delete", new DeleteForm { Reason = reason });
}
