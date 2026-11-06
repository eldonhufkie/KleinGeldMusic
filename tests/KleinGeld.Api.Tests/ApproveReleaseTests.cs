using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

public sealed partial class SubmitReleaseTests
{
    [Fact]
    public async Task Other_Members_get_not_found_and_refused_links_before_approval_and_both_succeed_after_it()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var otherMember = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateCompleteDraftAsync(manager);
        var track = (await ReadTracksAsync(manager, release)).Single();
        using var submitted = await SubmitAsync(manager, release);

        using var hiddenPage = await otherMember.GetAsync($"/api/releases/{release.Id}/{release.Slug}");
        using var hiddenTracks = await otherMember.GetAsync($"/api/tracks?releaseId={release.Id}");
        using var refusedLink = await otherMember.GetAsync($"/api/tracks/{track.Id}/play-link");
        Assert.Equal(HttpStatusCode.NotFound, hiddenPage.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, hiddenTracks.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, refusedLink.StatusCode);

        using var approved = await ApproveAsync(admin, release);

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal(ReleaseStage.Published, (await ReadAsync(otherMember, release)).Stage);
        Assert.Equal(track.Id, (await ReadTracksAsync(otherMember, release)).Single().Id);
        var link = await otherMember.GetFromJsonAsync<PlayLink>($"/api/tracks/{track.Id}/play-link");
        using var played = await BlobStorage.GetAsync(link!.Url);
        Assert.Equal(HttpStatusCode.OK, played.StatusCode);
    }

    [Fact]
    public async Task Approval_sets_the_Published_date()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateCompleteDraftAsync(manager);
        using var submitted = await SubmitAsync(manager, release);
        Assert.Null((await ReadAsync(manager, release)).PublishedAt);
        var approvedAt = new DateTimeOffset(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);
        await app.SetClockAsync(approvedAt);

        using var approved = await ApproveAsync(admin, release);
        await app.SetClockAsync(DateTimeOffset.UtcNow);

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal(approvedAt, (await ReadAsync(manager, release)).PublishedAt);
    }

    [Fact]
    public async Task Only_a_Submitted_Release_can_be_approved()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateCompleteDraftAsync(manager);

        using var draft = await ApproveAsync(admin, release);

        await AssertRefusedAsync(draft, HttpStatusCode.Conflict, "Only a Submitted Release");
        Assert.Equal(ReleaseStage.Draft, (await ReadAsync(manager, release)).Stage);

        using var submitted = await SubmitAsync(manager, release);
        using var approved = await ApproveAsync(admin, release);
        using var again = await ApproveAsync(admin, release);

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        await AssertRefusedAsync(again, HttpStatusCode.Conflict, "Only a Submitted Release");
    }

    [Fact]
    public async Task Approving_an_unknown_Release_answers_not_found()
    {
        using var admin = await app.LoginAsAdminAsync();

        using var response = await admin.PostAsync($"/api/admin/review/releases/{Guid.NewGuid()}/approve", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Only_the_Admin_approves_so_a_managing_Member_cannot_approve_their_own_Release()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var outsider = app.CreateWebClient();
        var release = await CreateCompleteDraftAsync(manager);
        using var submitted = await SubmitAsync(manager, release);

        using var byManager = await ApproveAsync(manager, release);
        using var notLoggedIn = await ApproveAsync(outsider, release);

        Assert.Equal(HttpStatusCode.Forbidden, byManager.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, notLoggedIn.StatusCode);
        Assert.Equal(ReleaseStage.Submitted, (await ReadAsync(manager, release)).Stage);
    }

    [Fact]
    public async Task An_approved_Release_leaves_the_Review_page()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateCompleteDraftAsync(manager);
        using var submitted = await SubmitAsync(manager, release);
        Assert.Contains(await ReadReviewAsync(admin), r => r.Id == release.Id);

        using var approved = await ApproveAsync(admin, release);

        Assert.DoesNotContain(await ReadReviewAsync(admin), r => r.Id == release.Id);
    }

    [Fact]
    public async Task Email_7_tells_every_managing_Member_their_Release_is_approved()
    {
        var (manager, _, managerEmail) = await LoginAsNewManagerAsync();
        var (fellow, fellowName, fellowEmail) = await LoginAsNewManagerAsync();
        using var managerClient = manager;
        using var fellowClient = fellow;
        using var admin = await app.LoginAsAdminAsync();
        var title = $"Goedgekeurd {Guid.NewGuid():N}";
        var release = await CreateCompleteDraftAsync(manager, title);
        await AddManagerAsync(manager, release, fellowName);
        using var submitted = await SubmitAsync(manager, release);

        using var approved = await ApproveAsync(admin, release);

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        foreach (var address in new[] { managerEmail, fellowEmail })
        {
            var email = await app.CreateMailPit().WaitForEmailAsync(address, title);
            Assert.Contains("De Inzenders", email.Text);
            Assert.Contains("Published", email.Text);
        }
    }

    [Fact]
    public async Task The_Admin_may_approve_their_own_Release_and_only_the_other_managers_get_email_7()
    {
        using var admin = await app.LoginAsAdminAsync();
        var (fellow, fellowName, fellowEmail) = await LoginAsNewManagerAsync();
        using var fellowClient = fellow;
        using var otherMember = await app.LoginAsNewMemberAsync();
        var title = $"Eigen Goedkeuring {Guid.NewGuid():N}";
        var release = await CreateCompleteDraftAsync(admin, title);
        await AddManagerAsync(admin, release, fellowName);
        using var submitted = await SubmitAsync(admin, release);

        using var approved = await ApproveAsync(admin, release);

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal(ReleaseStage.Published, (await ReadAsync(otherMember, release)).Stage);
        await app.CreateMailPit().WaitForEmailAsync(fellowEmail, title);
        await AssertNoEmailAsync(title);
    }

    private async Task<(HttpClient Client, string Name, string Email)> LoginAsNewManagerAsync()
    {
        var name = $"Manager {Guid.NewGuid():N}";
        var client = await app.LoginAsNewMemberAsync(name);
        return (client, name, (await client.GetFromJsonAsync<MeResponse>("/api/me"))!.Email);
    }

    private static async Task AddManagerAsync(HttpClient manager, ReleaseDetails release, string name)
    {
        var candidates = await manager.GetFromJsonAsync<List<ArtistManager>>($"/api/artists/{release.ArtistId}/managers/candidates");
        using var added = await manager.PostAsJsonAsync(
            $"/api/artists/{release.ArtistId}/managers",
            new AddManagerForm { MemberId = candidates!.Single(c => c.Name == name).Id });
        Assert.Equal(HttpStatusCode.NoContent, added.StatusCode);
    }

    private static Task<HttpResponseMessage> ApproveAsync(HttpClient member, ReleaseDetails release) =>
        member.PostAsync($"/api/admin/review/releases/{release.Id}/approve", null);

    private static async Task<List<TrackSummary>> ReadTracksAsync(HttpClient member, ReleaseDetails release) =>
        (await member.GetFromJsonAsync<List<TrackSummary>>($"/api/tracks?releaseId={release.Id}"))!;
}
