using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

public sealed partial class SubmitReleaseTests
{
    [Fact]
    public async Task Takedown_hides_the_Release_from_other_Members_and_badges_it_Taken_down_on_My_Artists()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var otherMember = await app.LoginAsNewMemberAsync();
        var release = await CreatePublishedAsync(manager);
        var track = (await ReadTracksAsync(manager, release)).Single();

        using var takenDown = await TakeDownAsync(manager, release);

        Assert.Equal(HttpStatusCode.OK, takenDown.StatusCode);
        using var hiddenPage = await otherMember.GetAsync($"/api/releases/{release.Id}/{release.Slug}");
        using var refusedLink = await otherMember.GetAsync($"/api/tracks/{track.Id}/play-link");
        Assert.Equal(HttpStatusCode.NotFound, hiddenPage.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, refusedLink.StatusCode);
        var artist = await manager.GetFromJsonAsync<ManagedArtist>($"/api/my-artists/{release.ArtistId}");
        Assert.Equal(ReleaseStage.TakenDown, artist!.Releases.Single(r => r.Id == release.Id).Stage);
    }

    [Fact]
    public async Task A_link_issued_before_Takedown_stops_working_and_a_fresh_link_for_another_Published_Track_works()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var listener = await app.LoginAsNewMemberAsync();
        var release = await CreatePublishedAsync(manager);
        var other = await CreatePublishedAsync(manager, "Blijft Staan");
        var track = (await ReadTracksAsync(manager, release)).Single();
        var otherTrack = (await ReadTracksAsync(manager, other)).Single();
        var before = await listener.GetFromJsonAsync<PlayLink>($"/api/tracks/{track.Id}/play-link");
        using (var playing = await BlobStorage.GetAsync(before!.Url))
        {
            Assert.Equal(HttpStatusCode.OK, playing.StatusCode);
        }

        using var takenDown = await TakeDownAsync(manager, release);

        using var revoked = await BlobStorage.GetAsync(before.Url);
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        var fresh = await listener.GetFromJsonAsync<PlayLink>($"/api/tracks/{otherTrack.Id}/play-link");
        using var playingOn = await BlobStorage.GetAsync(fresh!.Url);
        Assert.Equal(HttpStatusCode.OK, playingOn.StatusCode);
    }

    [Fact]
    public async Task Only_a_Published_Release_can_be_taken_down_and_only_by_its_managing_Members()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var otherMember = await app.LoginAsNewMemberAsync();
        var draft = await CreateCompleteDraftAsync(manager);
        var published = await CreatePublishedAsync(manager);

        using var ofDraft = await TakeDownAsync(manager, draft);
        using var byOther = await TakeDownAsync(otherMember, published);

        await AssertRefusedAsync(ofDraft, HttpStatusCode.Conflict, "Only a Published Release");
        Assert.Equal(HttpStatusCode.NotFound, byOther.StatusCode);
        Assert.Equal(ReleaseStage.Published, (await ReadAsync(otherMember, published)).Stage);
    }

    [Fact]
    public async Task A_Taken_down_Release_is_resubmitted_through_review_again()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var otherMember = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreatePublishedAsync(manager);
        using var takenDown = await TakeDownAsync(manager, release);

        using var resubmitted = await SubmitAsync(manager, release);

        Assert.Equal(HttpStatusCode.OK, resubmitted.StatusCode);
        Assert.Equal(ReleaseStage.Submitted, (await ReadAsync(manager, release)).Stage);
        Assert.Contains(await ReadReviewAsync(admin), r => r.Id == release.Id);
        using var stillHidden = await otherMember.GetAsync($"/api/releases/{release.Id}/{release.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, stillHidden.StatusCode);

        using var approved = await ApproveAsync(admin, release);

        Assert.Equal(ReleaseStage.Published, (await ReadAsync(otherMember, release)).Stage);
    }

    private async Task<ReleaseDetails> CreatePublishedAsync(HttpClient manager, string title = "Even Te Horen")
    {
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateCompleteDraftAsync(manager, title);
        using var submitted = await SubmitAsync(manager, release);
        using var approved = await ApproveAsync(admin, release);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        return release;
    }

    private static Task<HttpResponseMessage> TakeDownAsync(HttpClient member, ReleaseDetails release) =>
        member.PostAsync($"/api/releases/{release.Id}/takedown", null);
}
