using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

public sealed partial class SubmitReleaseTests
{
    [Fact]
    public async Task Deleting_a_Published_Release_removes_it_and_its_blobs_and_revokes_its_play_links()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var listener = await app.LoginAsNewMemberAsync();
        var release = await CreatePublishedAsync(manager);
        var track = (await ReadTracksAsync(manager, release)).Single();
        var before = await listener.GetFromJsonAsync<PlayLink>($"/api/tracks/{track.Id}/play-link");
        Assert.NotEmpty(await StoredBlobsAsync(release, track));
        var mailPit = app.CreateMailPit();
        var emailsBefore = (await mailPit.SearchAsync(KleinGeldApp.AdminEmail, release.Title)).Count;

        using var deleted = await manager.DeleteAsync($"/api/releases/{release.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await StoredBlobsAsync(release, track));
        using var page = await listener.GetAsync($"/api/releases/{release.Id}/{release.Slug}");
        using var link = await listener.GetAsync($"/api/tracks/{track.Id}/play-link");
        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, link.StatusCode);
        using var revoked = await BlobStorage.GetAsync(before!.Url);
        Assert.NotEqual(HttpStatusCode.OK, revoked.StatusCode);
        Assert.Equal(emailsBefore, (await mailPit.SearchAsync(KleinGeldApp.AdminEmail, release.Title)).Count);
    }

    [Fact]
    public async Task Deleting_an_Artist_deletes_all_its_Releases_and_their_blobs()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var otherMember = await app.LoginAsNewMemberAsync();
        var published = await CreatePublishedAsync(manager, "Al Gehoord");
        var draft = await CreateDraftForAsync(manager, published.ArtistId);
        var tracks = (await ReadTracksAsync(manager, published)).Concat(await ReadTracksAsync(manager, draft)).ToList();
        var before = await otherMember.GetFromJsonAsync<PlayLink>($"/api/tracks/{tracks[0].Id}/play-link");

        using var deleted = await manager.DeleteAsync($"/api/artists/{published.ArtistId}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await StoredBlobsAsync(published, tracks[0]));
        Assert.Empty(await StoredBlobsAsync(draft, tracks[1]));
        using var artistPage = await otherMember.GetAsync($"/api/artists/{published.ArtistId}");
        using var releasePage = await otherMember.GetAsync($"/api/releases/{published.Id}/{published.Slug}");
        using var managed = await manager.GetAsync($"/api/my-artists/{published.ArtistId}");
        using var draftForManager = await manager.GetAsync($"/api/releases/{draft.Id}/{draft.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, artistPage.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, releasePage.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, managed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, draftForManager.StatusCode);
        using var revoked = await BlobStorage.GetAsync(before!.Url);
        Assert.NotEqual(HttpStatusCode.OK, revoked.StatusCode);
    }

    [Fact]
    public async Task Only_its_managing_Members_delete_an_Artist()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var otherMember = await app.LoginAsNewMemberAsync();
        var release = await CreatePublishedAsync(manager);

        using var byOther = await otherMember.DeleteAsync($"/api/artists/{release.ArtistId}");

        Assert.Equal(HttpStatusCode.NotFound, byOther.StatusCode);
        Assert.Equal(ReleaseStage.Published, (await ReadAsync(otherMember, release)).Stage);
    }

    private static async Task<ReleaseDetails> CreateDraftForAsync(HttpClient manager, Guid artistId)
    {
        using var response = await manager.PostAsJsonAsync($"/api/artists/{artistId}/releases", CompleteForm("Nog Niet Klaar"));
        var draft = (await response.Content.ReadFromJsonAsync<ReleaseDetails>())!;
        await SetCoverAsync(manager, draft);
        var track = await AddTrackAsync(manager, draft, "tone.flac");
        await WaitUntilConvertedAsync(manager, draft, track);
        return draft;
    }

    /// <summary>Every audio blob of the Track and every cover blob of the Release still in storage.</summary>
    private async Task<List<string>> StoredBlobsAsync(ReleaseDetails release, TrackSummary track)
    {
        var blobs = await app.CreateBlobServiceClientAsync();
        var names = new List<string>();
        foreach (var (container, prefix) in new[] { ("covers", $"{release.Id}/"), ("audio", $"tracks/{track.Id}/") })
        {
            await foreach (var blob in blobs.GetBlobContainerClient(container).GetBlobsAsync(new Azure.Storage.Blobs.Models.GetBlobsOptions { Prefix = prefix }))
            {
                names.Add($"{container}/{blob.Name}");
            }
        }
        return names;
    }
}
