using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

// Tracks whose Release was taken down, rejected or deleted stay on Playlists as Unavailable Tracks.
public sealed partial class SubmitReleaseTests
{
    [Fact]
    public async Task A_taken_down_Track_stays_Unavailable_with_its_title_and_Artist_and_comes_back_in_place_when_Published_again()
    {
        using var admin = await app.LoginAsAdminAsync();
        var (member, playlist, releases, tracks) = await PlaylistOfThreeSinglesAsync();
        using var _ = member;
        var before = (await ReadPlaylistAsync(member, playlist)).Tracks[1];

        using (await TakeDownAsync(member, releases[1]))
        {
        }
        var takenDown = await ReadPlaylistAsync(member, playlist);

        var unavailable = takenDown.Tracks[1];
        Assert.False(unavailable.IsAvailable);
        Assert.Equal(before.Title, unavailable.Title);
        Assert.Equal(before.ArtistName, unavailable.ArtistName);
        Assert.Null(unavailable.ArtistId);
        Assert.Null(unavailable.ReleaseId);
        Assert.Equal(tracks[0].Duration + tracks[2].Duration, takenDown.TotalDuration);

        using (await SubmitAsync(member, releases[1]))
        using (await ApproveAsync(admin, releases[1]))
        {
        }
        var republished = await ReadPlaylistAsync(member, playlist);

        Assert.Equal(tracks.Select(t => (Guid?)t.Id), republished.Tracks.Select(t => t.TrackId));
        Assert.True(republished.Tracks[1].IsAvailable);
        Assert.Equal(releases[1].Id, republished.Tracks[1].ReleaseId);
    }

    [Fact]
    public async Task A_deleted_Track_stays_Unavailable_with_its_stored_title_and_Artist()
    {
        var (member, playlist, releases, _) = await PlaylistOfThreeSinglesAsync();
        using var _ = member;
        var before = (await ReadPlaylistAsync(member, playlist)).Tracks[0];

        using var deleted = await member.DeleteAsync($"/api/releases/{releases[0].Id}");
        var page = await ReadPlaylistAsync(member, playlist);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(3, page.Tracks.Count);
        var gone = page.Tracks[0];
        Assert.False(gone.IsAvailable);
        Assert.Null(gone.TrackId);
        Assert.Equal(before.Title, gone.Title);
        Assert.Equal(before.ArtistName, gone.ArtistName);
        Assert.Null(gone.ReleaseSlug);
    }

    [Fact]
    public async Task Remove_unavailable_Tracks_clears_them_all_and_keeps_the_rest_in_order()
    {
        var (member, playlist, releases, tracks) = await PlaylistOfThreeSinglesAsync();
        using var _ = member;
        using (await TakeDownAsync(member, releases[0]))
        using (await member.DeleteAsync($"/api/releases/{releases[2].Id}"))
        {
        }
        await app.SetClockAsync(DateTimeOffset.UtcNow.AddMinutes(5));

        using var cleared = await member.DeleteAsync($"/api/playlists/{playlist.Id}/unavailable");
        var page = await ReadPlaylistAsync(member, playlist);

        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Equal(2, (await cleared.Content.ReadFromJsonAsync<RemovedUnavailableTracks>())!.Removed);
        Assert.Equal([(Guid?)tracks[1].Id], page.Tracks.Select(t => t.TrackId));
        Assert.True(page.ChangedAt > playlist.ChangedAt);
    }

    [Fact]
    public async Task A_single_Unavailable_Track_can_be_removed_and_others_cannot_clear_a_Playlist()
    {
        var (member, playlist, releases, tracks) = await PlaylistOfThreeSinglesAsync();
        using var _ = member;
        using var other = await app.LoginAsNewMemberAsync();
        using (await member.DeleteAsync($"/api/releases/{releases[0].Id}"))
        {
        }
        var gone = (await ReadPlaylistAsync(member, playlist)).Tracks[0];

        using var byOther = await other.DeleteAsync($"/api/playlists/{playlist.Id}/unavailable");
        using var removed = await member.DeleteAsync($"/api/playlists/{playlist.Id}/entries/{gone.EntryId}");
        var page = await ReadPlaylistAsync(member, playlist);

        Assert.Equal(HttpStatusCode.NotFound, byOther.StatusCode);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Equal([(Guid?)tracks[1].Id, tracks[2].Id], page.Tracks.Select(t => t.TrackId));
    }

    private async Task<(HttpClient Member, PlaylistSummary Playlist, List<ReleaseDetails> Releases, List<TrackSummary> Tracks)> PlaylistOfThreeSinglesAsync()
    {
        using var admin = await app.LoginAsAdminAsync();
        var member = await app.LoginAsNewMemberAsync();
        var artist = await CreateArtistAsync(member);
        var releases = new List<ReleaseDetails>();
        var tracks = new List<TrackSummary>();
        for (var i = 0; i < 3; i++)
        {
            var release = await PublishAsync(member, admin, artist, ReleaseType.Single, new DateOnly(2024, 1, 1 + i));
            releases.Add(release);
            tracks.Add((await ReadTracksAsync(member, release))[0]);
        }
        var playlist = await CreatePlaylistAsync(member, "Onbeschikbaar");
        foreach (var track in tracks)
        {
            using var added = await AddToPlaylistAsync(member, playlist, track);
            Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        }
        return (member, playlist, releases, tracks);
    }
}
