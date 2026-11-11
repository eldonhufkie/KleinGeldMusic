using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

public sealed partial class SubmitReleaseTests
{
    [Fact]
    public async Task Adding_a_Track_to_a_Published_Release_starts_Draft_Pending_Changes_that_only_managers_and_the_Admin_play()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var otherMember = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreatePublishedAsync(manager, admin);
        var live = (await ReadTracksAsync(manager, release)).Single();

        var added = await AddTrackAsync(manager, release, "tone.mp3");
        await WaitUntilConvertedAsync(manager, release, added);

        Assert.Equal(ReleaseStage.Draft, await ReadPendingChangesAsync(manager, release));
        Assert.Equal(ReleaseStage.Published, (await ReadAsync(otherMember, release)).Stage);
        Assert.Null((await ReadAsync(otherMember, release)).PendingChanges);
        Assert.Equal(ReleaseStage.Draft, (await ReadAsync(manager, release)).PendingChanges);
        Assert.Equal(live.Id, (await ReadTracksAsync(otherMember, release)).Single().Id);
        using var refused = await otherMember.GetAsync($"/api/tracks/{added.Track.Id}/play-link");
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        foreach (var client in new[] { manager, admin })
        {
            var tracks = await ReadTracksAsync(client, release);
            Assert.True(tracks.Single(t => t.Id == added.Track.Id).Pending);
            Assert.False(tracks.Single(t => t.Id == live.Id).Pending);
            using var played = await client.GetAsync($"/api/tracks/{added.Track.Id}/play-link");
            Assert.Equal(HttpStatusCode.OK, played.StatusCode);
        }
    }

    [Fact]
    public async Task Submitting_Pending_Changes_needs_a_Rights_Declaration_and_puts_only_the_added_Tracks_on_the_Review_page()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var title = $"Uitbreiding {Guid.NewGuid():N}";
        var release = await CreatePublishedAsync(manager, admin, title);
        var added = await AddTrackAsync(manager, release, "tone.mp3");
        await WaitUntilConvertedAsync(manager, release, added);
        var countBefore = await ReadCountAsync(admin);

        using var undeclared = await SubmitPendingChangesAsync(manager, release, rightsDeclared: false);
        await AssertRefusedAsync(undeclared, HttpStatusCode.BadRequest, "Rights Declaration");

        using var submitted = await SubmitPendingChangesAsync(manager, release);

        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        Assert.Equal(ReleaseStage.Submitted, await ReadPendingChangesAsync(manager, release));
        Assert.Equal(ReleaseStage.Published, (await ReadAsync(manager, release)).Stage);
        Assert.Equal(countBefore + 1, await ReadCountAsync(admin));
        var review = Assert.Single(await ReadReviewAsync(admin), r => r.Id == release.Id);
        Assert.True(review.IsPendingChanges);
        Assert.Equal(added.Track.Id, Assert.Single(review.AddedTracks!).Id);
        // The first email 6 was for the Release itself.
        var emails = await app.CreateMailPit().WaitForEmailsAsync(KleinGeldApp.AdminEmail, title, 2);
        Assert.Contains(emails, e => e.Text.Contains("Pending Changes"));
    }

    [Fact]
    public async Task A_Submitted_set_of_Pending_Changes_is_locked()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreatePublishedAsync(manager, admin);
        await WaitUntilConvertedAsync(manager, release, await AddTrackAsync(manager, release, "tone.mp3"));
        using var submitted = await SubmitPendingChangesAsync(manager, release);

        var file = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tone.mp3"));
        using var another = await manager.PostAsJsonAsync($"/api/releases/{release.Id}/tracks", new TrackUploadForm { FileName = "tone.mp3", Size = file.Length });

        await AssertRefusedAsync(another, HttpStatusCode.Conflict, "Submitted");
    }

    [Fact]
    public async Task Approving_Pending_Changes_merges_the_Tracks_for_every_Member_and_sends_email_7()
    {
        var (manager, _, managerEmail) = await LoginAsNewManagerAsync();
        using var managerClient = manager;
        using var otherMember = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var title = $"Aangevuld {Guid.NewGuid():N}";
        var release = await CreatePublishedAsync(manager, admin, title);
        var publishedAt = (await ReadAsync(manager, release)).PublishedAt;
        var added = await AddTrackAsync(manager, release, "tone.mp3");
        await WaitUntilConvertedAsync(manager, release, added);
        using var submitted = await SubmitPendingChangesAsync(manager, release);

        using var approved = await ApproveAsync(admin, release);

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Null(await ReadPendingChangesAsync(manager, release));
        Assert.Equal(publishedAt, (await ReadAsync(manager, release)).PublishedAt);
        var tracks = await ReadTracksAsync(otherMember, release);
        Assert.Equal(2, tracks.Count);
        Assert.All(tracks, t => Assert.False(t.Pending));
        using var played = await otherMember.GetAsync($"/api/tracks/{added.Track.Id}/play-link");
        Assert.Equal(HttpStatusCode.OK, played.StatusCode);
        Assert.DoesNotContain(await ReadReviewAsync(admin), r => r.Id == release.Id);
        // The first email 7 was for the Release itself.
        var emails = await app.CreateMailPit().WaitForEmailsAsync(managerEmail, title, 2);
        Assert.Contains(emails, e => e.Text.Contains("Pending Changes"));
    }

    [Fact]
    public async Task Rejecting_Pending_Changes_keeps_the_Release_Published_and_shows_the_reason_on_My_Artists()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var otherMember = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreatePublishedAsync(manager, admin);
        await WaitUntilConvertedAsync(manager, release, await AddTrackAsync(manager, release, "tone.mp3"));
        using var submitted = await SubmitPendingChangesAsync(manager, release);

        using var rejected = await admin.PostAsJsonAsync($"/api/admin/review/releases/{release.Id}/reject", new RejectForm { Reason = "Te zacht." });

        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var managed = await ReadManagedAsync(manager, release);
        Assert.Equal(ReleaseStage.Published, managed.Stage);
        Assert.Equal(ReleaseStage.Rejected, managed.PendingChanges);
        Assert.Equal("Te zacht.", managed.PendingChangesRejectionReason);
        Assert.Single(await ReadTracksAsync(otherMember, release));
    }

    [Fact]
    public async Task Removing_the_last_added_Track_ends_the_Pending_Changes()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreatePublishedAsync(manager, admin);
        var live = (await ReadTracksAsync(manager, release)).Single();
        var added = await AddTrackAsync(manager, release, "tone.mp3");

        using var removeLive = await manager.DeleteAsync($"/api/releases/{release.Id}/tracks/{live.Id}");
        using var removeAdded = await manager.DeleteAsync($"/api/releases/{release.Id}/tracks/{added.Track.Id}");

        Assert.Equal(HttpStatusCode.Conflict, removeLive.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removeAdded.StatusCode);
        Assert.Null(await ReadPendingChangesAsync(manager, release));
    }

    private async Task<ReleaseDetails> CreatePublishedAsync(HttpClient manager, HttpClient admin, string title = "Al Uitgebracht")
    {
        var release = await CreateCompleteDraftAsync(manager, title);
        using var submitted = await SubmitAsync(manager, release);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        using var approved = await ApproveAsync(admin, release);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        return release;
    }

    private static Task<HttpResponseMessage> SubmitPendingChangesAsync(HttpClient member, ReleaseDetails release, bool rightsDeclared = true) =>
        member.PostAsJsonAsync($"/api/releases/{release.Id}/pending-changes/submit", new SubmitForm { RightsDeclared = rightsDeclared });

    private static async Task<ManagedRelease> ReadManagedAsync(HttpClient manager, ReleaseDetails release)
    {
        var artist = await manager.GetFromJsonAsync<ManagedArtist>($"/api/my-artists/{release.ArtistId}/de-inzenders");
        return artist!.Releases.Single(r => r.Id == release.Id);
    }

    private static async Task<ReleaseStage?> ReadPendingChangesAsync(HttpClient manager, ReleaseDetails release) =>
        (await ReadManagedAsync(manager, release)).PendingChanges;
}
