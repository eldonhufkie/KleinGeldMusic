using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

// Reordering, removing with Undo, renaming and deleting a Playlist, and its automatic cover.
public sealed partial class SubmitReleaseTests
{
    [Fact]
    public async Task Moving_a_Track_saves_the_new_order_and_bumps_the_Playlist()
    {
        var (member, playlist, tracks) = await PlaylistWithThreeTracksAsync();
        using var _ = member;
        var before = (await ReadPlaylistAsync(member, playlist)).Tracks;
        await app.SetClockAsync(DateTimeOffset.UtcNow.AddMinutes(5));

        using var response = await member.PutAsJsonAsync(
            $"/api/playlists/{playlist.Id}/entries/{before[2].EntryId}/position", new MovePlaylistEntryForm { Position = 1 });
        var page = await ReadPlaylistAsync(member, playlist);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([tracks[2].Id, tracks[0].Id, tracks[1].Id], page.Tracks.Select(t => t.TrackId));
        Assert.True(page.ChangedAt > playlist.ChangedAt);
    }

    [Fact]
    public async Task A_removed_Track_can_be_put_back_in_the_same_place()
    {
        var (member, playlist, tracks) = await PlaylistWithThreeTracksAsync();
        using var _ = member;
        var middle = (await ReadPlaylistAsync(member, playlist)).Tracks[1];

        using var removed = await member.DeleteAsync($"/api/playlists/{playlist.Id}/entries/{middle.EntryId}");
        var afterRemove = await ReadPlaylistAsync(member, playlist);
        var undo = (await removed.Content.ReadFromJsonAsync<RemovedPlaylistEntry>())!;
        using var restored = await member.PostAsJsonAsync($"/api/playlists/{playlist.Id}/entries/restore", undo);
        var afterUndo = await ReadPlaylistAsync(member, playlist);

        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Equal([tracks[0].Id, tracks[2].Id], afterRemove.Tracks.Select(t => t.TrackId));
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.Equal(tracks.Select(t => (Guid?)t.Id), afterUndo.Tracks.Select(t => t.TrackId));
    }

    [Fact]
    public async Task A_Playlist_can_be_renamed_and_its_slug_follows()
    {
        using var member = await app.LoginAsNewMemberAsync();
        var playlist = await CreatePlaylistAsync(member, "Oude Naam");

        using var response = await member.PutAsJsonAsync($"/api/playlists/{playlist.Id}", new PlaylistForm { Name = "Nieuwe Naam" });
        var renamed = (await response.Content.ReadFromJsonAsync<PlaylistSummary>())!;
        using var blank = await member.PutAsJsonAsync($"/api/playlists/{playlist.Id}", new PlaylistForm { Name = " " });

        Assert.Equal("Nieuwe Naam", renamed.Name);
        Assert.Equal("nieuwe-naam", renamed.Slug);
        Assert.Equal("Nieuwe Naam", (await ReadPlaylistAsync(member, renamed)).Name);
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
    }

    [Fact]
    public async Task A_deleted_Playlist_is_gone()
    {
        var (member, playlist, _) = await PlaylistWithThreeTracksAsync();
        using var __ = member;

        using var response = await member.DeleteAsync($"/api/playlists/{playlist.Id}");
        using var read = await member.GetAsync($"/api/playlists/{playlist.Id}/{playlist.Slug}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.DoesNotContain(await ReadPlaylistsAsync(member), p => p.Id == playlist.Id);
    }

    [Fact]
    public async Task Someone_else_cannot_edit_a_Playlist()
    {
        var (owner, playlist, _) = await PlaylistWithThreeTracksAsync();
        using var _ = owner;
        using var other = await app.LoginAsNewMemberAsync();
        var entry = (await ReadPlaylistAsync(owner, playlist)).Tracks[0];

        using var move = await other.PutAsJsonAsync(
            $"/api/playlists/{playlist.Id}/entries/{entry.EntryId}/position", new MovePlaylistEntryForm { Position = 2 });
        using var remove = await other.DeleteAsync($"/api/playlists/{playlist.Id}/entries/{entry.EntryId}");
        using var rename = await other.PutAsJsonAsync($"/api/playlists/{playlist.Id}", new PlaylistForm { Name = "Van Mij" });
        using var delete = await other.DeleteAsync($"/api/playlists/{playlist.Id}");

        Assert.All([move, remove, rename, delete], r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
        Assert.Equal(3, (await ReadPlaylistAsync(owner, playlist)).Tracks.Count);
    }

    [Fact]
    public async Task The_cover_is_the_cover_of_the_first_playable_Tracks_Release()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var artist = await CreateArtistAsync(manager);
        var first = await PublishAsync(manager, admin, artist, ReleaseType.Single, new DateOnly(2024, 1, 1));
        var second = await PublishAsync(manager, admin, artist, ReleaseType.Single, new DateOnly(2024, 2, 1));
        var playlist = await CreatePlaylistAsync(manager, "Hoes");
        Assert.Null((await ReadPlaylistAsync(manager, playlist)).Cover);
        using (await AddToPlaylistAsync(manager, playlist, (await ReadTracksAsync(manager, second))[0]))
        using (await AddToPlaylistAsync(manager, playlist, (await ReadTracksAsync(manager, first))[0]))
        {
        }

        var page = await ReadPlaylistAsync(manager, playlist);
        Assert.Contains(second.Id.ToString(), page.Cover!.Small.AbsolutePath);

        using (await MovePlaylistEntryAsync(manager, playlist, page.Tracks[1].EntryId, 1))
        {
        }
        Assert.Contains(first.Id.ToString(), (await ReadPlaylistAsync(manager, playlist)).Cover!.Small.AbsolutePath);
    }

    private static Task<HttpResponseMessage> MovePlaylistEntryAsync(HttpClient member, PlaylistSummary playlist, Guid entryId, int position) =>
        member.PutAsJsonAsync($"/api/playlists/{playlist.Id}/entries/{entryId}/position", new MovePlaylistEntryForm { Position = position });

    private async Task<(HttpClient Member, PlaylistSummary Playlist, List<TrackSummary> Tracks)> PlaylistWithThreeTracksAsync()
    {
        using var admin = await app.LoginAsAdminAsync();
        var member = await app.LoginAsNewMemberAsync();
        var artist = await CreateArtistAsync(member);
        var tracks = new List<TrackSummary>();
        for (var i = 0; i < 3; i++)
        {
            var release = await PublishAsync(member, admin, artist, ReleaseType.Single, new DateOnly(2024, 1, 1 + i));
            tracks.Add((await ReadTracksAsync(member, release))[0]);
        }
        var playlist = await CreatePlaylistAsync(member, "Volgorde");
        foreach (var track in tracks)
        {
            using var added = await AddToPlaylistAsync(member, playlist, track);
            Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        }
        return (member, playlist, tracks);
    }
}
