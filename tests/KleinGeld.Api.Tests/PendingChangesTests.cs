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

        using var removeAdded = await manager.DeleteAsync($"/api/releases/{release.Id}/tracks/{added.Track.Id}");

        Assert.Equal(HttpStatusCode.NoContent, removeAdded.StatusCode);
        Assert.Null(await ReadPendingChangesAsync(manager, release));
        Assert.Equal(live.Id, (await ReadTracksAsync(manager, release)).Single().Id);
    }

    [Fact]
    public async Task Replacing_a_Published_Tracks_audio_keeps_the_Track_and_Members_hear_the_old_audio_until_approval()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var otherMember = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreatePublishedAsync(manager, admin);
        var track = (await ReadTracksAsync(manager, release)).Single();
        var oldAudio = await PlayedBlobAsync(otherMember, track.Id);

        var replacement = await ReplaceAudioAsync(manager, release, track.Id, "tone.flac");
        await WaitUntilConvertedAsync(manager, release, replacement);

        Assert.Equal(track.Id, replacement.Track.Id);
        Assert.Equal(ReleaseStage.Draft, await ReadPendingChangesAsync(manager, release));
        Assert.Equal(oldAudio, await PlayedBlobAsync(otherMember, track.Id));
        var memberView = (await ReadTracksAsync(otherMember, release)).Single();
        Assert.Equal(TrackStatus.Ready, memberView.Status);
        Assert.False(memberView.Replaced);
        Assert.True((await ReadTracksAsync(manager, release)).Single().Replaced);
        var newAudio = await PlayedBlobAsync(admin, track.Id);
        Assert.NotEqual(oldAudio, newAudio);
        Assert.Equal(oldAudio, await PlayedBlobAsync(admin, track.Id, old: true));
        Assert.Equal(newAudio, await PlayedBlobAsync(manager, track.Id));
        Assert.Equal(oldAudio, await PlayedBlobAsync(otherMember, track.Id, old: true));
    }

    [Fact]
    public async Task Removing_a_Published_Track_is_a_Pending_Change_that_Members_dont_notice_until_approval()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var otherMember = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreatePublishedAsync(manager, admin);
        var kept = (await ReadTracksAsync(manager, release)).Single();
        var gone = await AddApprovedTrackAsync(manager, admin, release);

        using var removed = await manager.DeleteAsync($"/api/releases/{release.Id}/tracks/{gone.Track.Id}");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(ReleaseStage.Draft, await ReadPendingChangesAsync(manager, release));
        Assert.Equal(2, (await ReadTracksAsync(otherMember, release)).Count);
        Assert.All(await ReadTracksAsync(otherMember, release), t => Assert.False(t.Removed));
        Assert.True((await ReadTracksAsync(manager, release)).Single(t => t.Id == gone.Track.Id).Removed);
        using var stillPlays = await otherMember.GetAsync($"/api/tracks/{gone.Track.Id}/play-link");
        Assert.Equal(HttpStatusCode.OK, stillPlays.StatusCode);
        Assert.Equal(kept.Id, (await ReadTracksAsync(manager, release)).Single(t => !t.Removed).Id);
    }

    [Fact]
    public async Task The_Review_page_shows_removed_and_replaced_Tracks()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreatePublishedAsync(manager, admin);
        var replaced = (await ReadTracksAsync(manager, release)).Single();
        var gone = await AddApprovedTrackAsync(manager, admin, release);
        await WaitUntilConvertedAsync(manager, release, await ReplaceAudioAsync(manager, release, replaced.Id, "tone.flac"));
        (await manager.DeleteAsync($"/api/releases/{release.Id}/tracks/{gone.Track.Id}")).Dispose();

        using var submitted = await SubmitPendingChangesAsync(manager, release);

        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var review = Assert.Single(await ReadReviewAsync(admin), r => r.Id == release.Id);
        Assert.True(review.IsPendingChanges);
        Assert.Empty(review.AddedTracks!);
        Assert.Equal(gone.Track.Id, Assert.Single(review.RemovedTracks!).Id);
        Assert.Equal(replaced.Id, Assert.Single(review.ReplacedTracks!).Id);
    }

    [Fact]
    public async Task Approving_removes_Tracks_and_old_audio_for_Members_and_revokes_their_links()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var otherMember = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreatePublishedAsync(manager, admin);
        var replaced = (await ReadTracksAsync(manager, release)).Single();
        var gone = await AddApprovedTrackAsync(manager, admin, release);
        var oldLink = await otherMember.GetFromJsonAsync<PlayLink>($"/api/tracks/{replaced.Id}/play-link");
        var goneLink = await otherMember.GetFromJsonAsync<PlayLink>($"/api/tracks/{gone.Track.Id}/play-link");
        await WaitUntilConvertedAsync(manager, release, await ReplaceAudioAsync(manager, release, replaced.Id, "tone.flac"));
        var newAudio = await PlayedBlobAsync(manager, replaced.Id);
        (await manager.DeleteAsync($"/api/releases/{release.Id}/tracks/{gone.Track.Id}")).Dispose();
        (await SubmitPendingChangesAsync(manager, release)).Dispose();

        using var approved = await ApproveAsync(admin, release);

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Null(await ReadPendingChangesAsync(manager, release));
        var track = Assert.Single(await ReadTracksAsync(otherMember, release));
        Assert.Equal(replaced.Id, track.Id);
        Assert.Equal(1, track.Position);
        Assert.Equal(newAudio, await PlayedBlobAsync(otherMember, replaced.Id));
        using var goneRefused = await otherMember.GetAsync($"/api/tracks/{gone.Track.Id}/play-link");
        Assert.Equal(HttpStatusCode.NotFound, goneRefused.StatusCode);
        using var oldRevoked = await BlobStorage.GetAsync(oldLink!.Url);
        using var goneRevoked = await BlobStorage.GetAsync(goneLink!.Url);
        Assert.NotEqual(HttpStatusCode.OK, oldRevoked.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, goneRevoked.StatusCode);
    }

    [Fact]
    public async Task Pending_Changes_can_be_Withdrawn_to_Draft_and_stay_editable_when_Rejected()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreatePublishedAsync(manager, admin);
        var track = (await ReadTracksAsync(manager, release)).Single();
        await WaitUntilConvertedAsync(manager, release, await ReplaceAudioAsync(manager, release, track.Id, "tone.flac"));
        (await SubmitPendingChangesAsync(manager, release)).Dispose();

        using var withdrawn = await manager.PostAsync($"/api/releases/{release.Id}/pending-changes/withdraw", null);

        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        Assert.Equal(ReleaseStage.Draft, await ReadPendingChangesAsync(manager, release));
        (await SubmitPendingChangesAsync(manager, release)).Dispose();
        (await admin.PostAsJsonAsync($"/api/admin/review/releases/{release.Id}/reject", new RejectForm { Reason = "Ruis." })).Dispose();
        var again = await ReplaceAudioAsync(manager, release, track.Id, "tone.mp3");
        Assert.Equal(TrackStatus.Ready, await WaitUntilConvertedAsync(manager, release, again));
        Assert.Equal(ReleaseStage.Rejected, await ReadPendingChangesAsync(manager, release));
        using var resubmitted = await SubmitPendingChangesAsync(manager, release);
        Assert.Equal(HttpStatusCode.OK, resubmitted.StatusCode);
    }

    [Fact]
    public async Task A_Takedown_folds_the_Pending_Changes_into_the_Release_for_whole_review_on_resubmission()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreatePublishedAsync(manager, admin);
        var replaced = (await ReadTracksAsync(manager, release)).Single();
        var gone = await AddApprovedTrackAsync(manager, admin, release);
        await WaitUntilConvertedAsync(manager, release, await ReplaceAudioAsync(manager, release, replaced.Id, "tone.flac"));
        var newAudio = await PlayedBlobAsync(manager, replaced.Id);
        (await manager.DeleteAsync($"/api/releases/{release.Id}/tracks/{gone.Track.Id}")).Dispose();
        var added = await AddTrackAsync(manager, release, "tone.mp3");
        await WaitUntilConvertedAsync(manager, release, added);
        (await SubmitPendingChangesAsync(manager, release)).Dispose();

        using var takenDown = await TakeDownAsync(manager, release);

        Assert.Equal(HttpStatusCode.OK, takenDown.StatusCode);
        Assert.Null(await ReadPendingChangesAsync(manager, release));
        Assert.DoesNotContain(await ReadReviewAsync(admin), r => r.Id == release.Id);
        var tracks = await ReadTracksAsync(manager, release);
        Assert.Equal([replaced.Id, added.Track.Id], tracks.OrderBy(t => t.Position).Select(t => t.Id));
        Assert.All(tracks, t => Assert.False(t.Pending || t.Removed || t.Replaced));
        Assert.Equal(newAudio, await PlayedBlobAsync(manager, replaced.Id));
        using var resubmitted = await SubmitAsync(manager, release);
        Assert.Equal(HttpStatusCode.OK, resubmitted.StatusCode);
        var review = Assert.Single(await ReadReviewAsync(admin), r => r.Id == release.Id);
        Assert.False(review.IsPendingChanges);
    }

    [Fact]
    public async Task The_Admin_Rejecting_a_Published_Release_folds_its_Pending_Changes_into_it()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreatePublishedAsync(manager, admin);
        var replaced = (await ReadTracksAsync(manager, release)).Single();
        var gone = await AddApprovedTrackAsync(manager, admin, release);
        await WaitUntilConvertedAsync(manager, release, await ReplaceAudioAsync(manager, release, replaced.Id, "tone.flac"));
        var newAudio = await PlayedBlobAsync(manager, replaced.Id);
        (await manager.DeleteAsync($"/api/releases/{release.Id}/tracks/{gone.Track.Id}")).Dispose();
        var added = await AddTrackAsync(manager, release, "tone.mp3");
        await WaitUntilConvertedAsync(manager, release, added);
        (await SubmitPendingChangesAsync(manager, release)).Dispose();

        using var rejected = await admin.PostAsJsonAsync($"/api/admin/releases/{release.Id}/reject", new RejectForm { Reason = "Weg." });

        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Null(await ReadPendingChangesAsync(manager, release));
        Assert.DoesNotContain(await ReadReviewAsync(admin), r => r.Id == release.Id);
        var tracks = await ReadTracksAsync(manager, release);
        Assert.Equal([replaced.Id, added.Track.Id], tracks.OrderBy(t => t.Position).Select(t => t.Id));
        Assert.All(tracks, t => Assert.False(t.Pending || t.Removed || t.Replaced));
        Assert.Equal(newAudio, await PlayedBlobAsync(manager, replaced.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deleting_a_Published_Release_discards_its_Pending_Changes_with_the_old_and_new_audio(bool byAdmin)
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreatePublishedAsync(manager, admin);
        var replaced = (await ReadTracksAsync(manager, release)).Single();
        await WaitUntilConvertedAsync(manager, release, await ReplaceAudioAsync(manager, release, replaced.Id, "tone.flac"));
        var added = await AddTrackAsync(manager, release, "tone.mp3");
        await WaitUntilConvertedAsync(manager, release, added);
        var addedTrack = (await ReadTracksAsync(manager, release)).Single(t => t.Id == added.Track.Id);
        var oldAudio = await PlayedBlobAsync(manager, replaced.Id, old: true);
        var newAudio = await PlayedBlobAsync(manager, replaced.Id);
        Assert.NotEqual(oldAudio, newAudio);

        using var deleted = byAdmin
            ? await admin.PostAsJsonAsync($"/api/admin/releases/{release.Id}/delete", new DeleteForm { Reason = "Weg." })
            : await manager.DeleteAsync($"/api/releases/{release.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await StoredBlobsAsync(release, replaced));
        Assert.Empty(await StoredBlobsAsync(release, addedTrack));
        Assert.DoesNotContain(await ReadReviewAsync(admin), r => r.Id == release.Id);
    }

    /// <summary>Adds a Track as Pending Changes and has the Admin approve it, so it is one of the Release's own.</summary>
    private async Task<TrackUpload> AddApprovedTrackAsync(HttpClient manager, HttpClient admin, ReleaseDetails release)
    {
        var added = await AddTrackAsync(manager, release, "tone.mp3");
        await WaitUntilConvertedAsync(manager, release, added);
        using var submitted = await SubmitPendingChangesAsync(manager, release);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        using var approved = await ApproveAsync(admin, release);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        return added;
    }

    private static async Task<TrackUpload> ReplaceAudioAsync(HttpClient manager, ReleaseDetails release, Guid trackId, string fixture)
    {
        var file = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture));
        using var response = await manager.PostAsJsonAsync(
            $"/api/releases/{release.Id}/tracks/{trackId}/replace", new TrackUploadForm { FileName = fixture, Size = file.Length });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var upload = (await response.Content.ReadFromJsonAsync<TrackUpload>())!;
        await PutBlobAsync(upload.UploadLink, file);
        using var confirmed = await manager.PostAsync($"/api/releases/{release.Id}/tracks/{trackId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        return upload;
    }

    /// <summary>The blob a play link reads: its path is the container, then the blob name.</summary>
    private static async Task<string> PlayedBlobAsync(HttpClient member, Guid trackId, bool old = false)
    {
        var link = await member.GetFromJsonAsync<PlayLink>($"/api/tracks/{trackId}/play-link{(old ? "?old=true" : "")}");
        return Uri.UnescapeDataString(link!.Url.AbsolutePath).Split("/audio/")[1];
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
