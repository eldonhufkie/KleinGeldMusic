using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

public sealed partial class SubmitReleaseTests
{
    [Fact]
    public async Task An_edit_to_a_Published_Release_is_visible_to_other_Members_at_once_and_it_stays_Published()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var otherMember = await app.LoginAsNewMemberAsync();
        var release = await CreatePublishedReleaseAsync(manager);

        using var edited = await manager.PutAsJsonAsync($"/api/releases/{release.Id}", new ReleaseForm
        {
            Title = "Een Nieuwe Naam",
            Type = ReleaseType.EP,
            ReleaseDate = new DateOnly(2025, 5, 1),
            Description = "Herzien",
            Genre = "Jazz",
        });

        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var seen = await otherMember.GetFromJsonAsync<ReleaseDetails>($"/api/releases/{release.Id}/een-nieuwe-naam");
        Assert.Equal("Een Nieuwe Naam", seen!.Title);
        Assert.Equal(ReleaseType.EP, seen.Type);
        Assert.Equal(new DateOnly(2025, 5, 1), seen.ReleaseDate);
        Assert.Equal("Herzien", seen.Description);
        Assert.Equal("Jazz", seen.Genre);
        Assert.Equal(ReleaseStage.Published, seen.Stage);
    }

    [Fact]
    public async Task Renaming_a_Published_Release_redirects_its_old_URL()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreatePublishedReleaseAsync(manager);

        using var edited = await manager.PutAsJsonAsync($"/api/releases/{release.Id}", CompleteForm("Andere Titel"));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);

        using var member = await app.LoginAsNewMemberAsync();
        using var old = await member.GetAsync($"/api/releases/{release.Id}/{release.Slug}");
        Assert.Equal(HttpStatusCode.OK, old.StatusCode);
        Assert.EndsWith($"/api/releases/{release.Id}/andere-titel", old.RequestMessage!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Track_details_and_the_cover_of_a_Published_Release_change_at_once()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var otherMember = await app.LoginAsNewMemberAsync();
        var release = await CreatePublishedReleaseAsync(manager);
        var track = (await otherMember.GetFromJsonAsync<List<TrackSummary>>($"/api/tracks?releaseId={release.Id}"))!.Single();
        var oldCover = (await ReadAsync(otherMember, release)).Cover;

        using var edited = await manager.PutAsJsonAsync(
            $"/api/releases/{release.Id}/tracks/{track.Id}",
            new TrackForm { Title = "Nieuwe Tracknaam", Explicit = true, FeaturedArtists = "Gast" });
        await SetCoverAsync(manager, release);

        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var seen = (await otherMember.GetFromJsonAsync<List<TrackSummary>>($"/api/tracks?releaseId={release.Id}"))!.Single();
        Assert.Equal("Nieuwe Tracknaam", seen.Title);
        Assert.True(seen.Explicit);
        Assert.Equal("Gast", seen.FeaturedArtists);
        var after = await ReadAsync(otherMember, release);
        Assert.NotEqual(oldCover, after.Cover);
        Assert.Equal(ReleaseStage.Published, after.Stage);
    }

    private async Task<ReleaseDetails> CreatePublishedReleaseAsync(HttpClient manager)
    {
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateCompleteDraftAsync(manager);
        using var submitted = await SubmitAsync(manager, release);
        using var approved = await admin.PostAsync($"/api/admin/review/releases/{release.Id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        return release;
    }
}
