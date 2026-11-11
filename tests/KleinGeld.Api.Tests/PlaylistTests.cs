using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

// Part of SubmitReleaseTests for its helpers: a Track goes on a Playlist once its Release has gone through approval.
public sealed partial class SubmitReleaseTests
{
    [Fact]
    public async Task A_new_Playlist_has_its_name_and_no_Tracks()
    {
        using var member = await app.LoginAsNewMemberAsync();

        var playlist = await CreatePlaylistAsync(member, "Zondagochtend");
        var page = await ReadPlaylistAsync(member, playlist);

        Assert.Equal("Zondagochtend", page.Name);
        Assert.Equal("zondagochtend", page.Slug);
        Assert.Empty(page.Tracks);
        Assert.Equal(TimeSpan.Zero, page.TotalDuration);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_Playlist_needs_a_name(string name)
    {
        using var member = await app.LoginAsNewMemberAsync();

        using var response = await member.PostAsJsonAsync("/api/playlists", new PlaylistForm { Name = name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_Playlist_name_is_at_most_100_characters()
    {
        using var member = await app.LoginAsNewMemberAsync();

        using var tooLong = await member.PostAsJsonAsync("/api/playlists", new PlaylistForm { Name = new string('a', 101) });
        using var longest = await member.PostAsJsonAsync("/api/playlists", new PlaylistForm { Name = new string('a', 100) });

        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.Created, longest.StatusCode);
    }

    [Fact]
    public async Task Two_Playlists_may_share_a_name()
    {
        using var member = await app.LoginAsNewMemberAsync();

        var first = await CreatePlaylistAsync(member, "Dubbel");
        var second = await CreatePlaylistAsync(member, "Dubbel");

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, (await ReadPlaylistsAsync(member)).Count(p => p.Name == "Dubbel"));
    }

    [Fact]
    public async Task Added_Tracks_go_at_the_end_with_their_title_Artist_and_Release()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        using var listener = await app.LoginAsNewMemberAsync();
        var artist = await CreateArtistAsync(manager);
        var release = await CreateReleaseAsync(manager, artist, ReleaseType.EP, new DateOnly(2024, 3, 1));
        await SetCoverAsync(manager, release);
        await WaitUntilConvertedAsync(manager, release, await AddTrackAsync(manager, release, "tone.flac"));
        await WaitUntilConvertedAsync(manager, release, await AddTrackAsync(manager, release, "tone.mp3"));
        using (await SubmitAsync(manager, release))
        using (await ApproveAsync(admin, release))
        {
        }
        var tracks = await ReadTracksAsync(listener, release);
        var playlist = await CreatePlaylistAsync(listener, "Lijst");

        using var second = await AddToPlaylistAsync(listener, playlist, tracks[1]);
        using var first = await AddToPlaylistAsync(listener, playlist, tracks[0]);
        var page = await ReadPlaylistAsync(listener, playlist);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal([tracks[1].Id, tracks[0].Id], page.Tracks.Select(t => t.TrackId));
        var entry = page.Tracks[0];
        Assert.Equal(tracks[1].Title, entry.Title);
        Assert.Equal(artist.Name, entry.ArtistName);
        Assert.Equal(artist.Id, entry.ArtistId);
        Assert.Equal(release.Id, entry.ReleaseId);
        Assert.Equal(release.Slug, entry.ReleaseSlug);
        Assert.True(entry.IsAvailable);
        Assert.Equal(tracks[0].Duration + tracks[1].Duration, page.TotalDuration);
    }

    [Fact]
    public async Task A_Track_already_on_the_Playlist_is_not_added_again()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await PublishAsync(manager, admin, await CreateArtistAsync(manager), ReleaseType.Single, new DateOnly(2024, 1, 1));
        var track = (await ReadTracksAsync(manager, release))[0];
        var playlist = await CreatePlaylistAsync(manager, "Eenmaal");
        using var added = await AddToPlaylistAsync(manager, playlist, track);

        using var again = await AddToPlaylistAsync(manager, playlist, track);

        await AssertRefusedAsync(again, HttpStatusCode.Conflict, "Already on Eenmaal");
        Assert.Single((await ReadPlaylistAsync(manager, playlist)).Tracks);
    }

    [Fact]
    public async Task A_Track_the_Member_cannot_see_cannot_be_added()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var listener = await app.LoginAsNewMemberAsync();
        var draft = await CreateCompleteDraftAsync(manager);
        var track = (await ReadTracksAsync(manager, draft))[0];
        var playlist = await CreatePlaylistAsync(listener, "Stiekem");

        using var response = await AddToPlaylistAsync(listener, playlist, track);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Another_Member_and_the_Admin_get_not_found_for_someone_elses_Playlist()
    {
        using var owner = await app.LoginAsNewMemberAsync();
        using var other = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await PublishAsync(manager, admin, await CreateArtistAsync(manager), ReleaseType.Single, new DateOnly(2024, 1, 1));
        var track = (await ReadTracksAsync(owner, release))[0];
        var playlist = await CreatePlaylistAsync(owner, "Van Mij");

        foreach (var stranger in new[] { other, admin })
        {
            using var read = await stranger.GetAsync($"/api/playlists/{playlist.Id}/{playlist.Slug}");
            using var add = await AddToPlaylistAsync(stranger, playlist, track);

            Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, add.StatusCode);
            Assert.DoesNotContain(await ReadPlaylistsAsync(stranger), p => p.Id == playlist.Id);
        }
    }

    [Fact]
    public async Task A_wrong_slug_redirects_to_the_Playlist()
    {
        using var member = await app.LoginAsNewMemberAsync();
        var playlist = await CreatePlaylistAsync(member, "Juiste Naam");

        var page = await member.GetFromJsonAsync<PlaylistDetails>($"/api/playlists/{playlist.Id}/oude-naam");

        Assert.Equal(playlist.Id, page!.Id);
    }

    [Fact]
    public async Task Playlists_are_listed_most_recently_changed_first()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await PublishAsync(manager, admin, await CreateArtistAsync(manager), ReleaseType.Single, new DateOnly(2024, 1, 1));
        var track = (await ReadTracksAsync(manager, release))[0];
        var now = DateTimeOffset.UtcNow;
        var older = await CreatePlaylistAsync(manager, "Ouder");
        await app.SetClockAsync(now.AddMinutes(1));
        var newer = await CreatePlaylistAsync(manager, "Nieuwer");
        Assert.Equal([newer.Id, older.Id], (await ReadPlaylistsAsync(manager)).Select(p => p.Id));

        await app.SetClockAsync(now.AddMinutes(2));
        using var added = await AddToPlaylistAsync(manager, older, track);
        var playlists = await ReadPlaylistsAsync(manager);

        Assert.Equal([older.Id, newer.Id], playlists.Select(p => p.Id));
        Assert.Equal(1, playlists[0].TrackCount);
    }

    [Fact]
    public async Task The_sidebar_lists_the_Members_own_Playlists_under_Playlists()
    {
        using var member = await app.LoginAsNewMemberAsync();
        using var other = await app.LoginAsNewMemberAsync();
        var mine = await CreatePlaylistAsync(member, $"Mijn {Guid.NewGuid():N}");
        var theirs = await CreatePlaylistAsync(other, $"Hun {Guid.NewGuid():N}");

        var page = await member.GetStringAsync("/Account/Manage");

        var sidebar = page[page.IndexOf("<nav class=\"sidebar\"", StringComparison.Ordinal)..];
        sidebar = sidebar[..sidebar.IndexOf("</nav>", StringComparison.Ordinal)];
        Assert.Contains("href=\"playlists\"", sidebar);
        Assert.Contains($"href=\"playlists/{mine.Id}/{mine.Slug}\"", sidebar);
        Assert.DoesNotContain(theirs.Name, sidebar);
    }

    [Fact]
    public async Task Adding_a_whole_Release_appends_its_missing_Tracks_in_order_and_reports_the_skipped()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateReleaseAsync(manager, await CreateArtistAsync(manager), ReleaseType.EP, new DateOnly(2024, 3, 1));
        await SetCoverAsync(manager, release);
        await WaitUntilConvertedAsync(manager, release, await AddTrackAsync(manager, release, "tone.flac"));
        await WaitUntilConvertedAsync(manager, release, await AddTrackAsync(manager, release, "tone.mp3"));
        await WaitUntilConvertedAsync(manager, release, await AddTrackAsync(manager, release, "tone.flac"));
        using (await SubmitAsync(manager, release))
        using (await ApproveAsync(admin, release))
        {
        }
        var tracks = await ReadTracksAsync(manager, release);
        var playlist = await CreatePlaylistAsync(manager, "Heel");
        using var already = await AddToPlaylistAsync(manager, playlist, tracks[1]);

        using var response = await manager.PostAsJsonAsync(
            $"/api/playlists/{playlist.Id}/releases", new AddReleaseToPlaylistForm { ReleaseId = release.Id });
        var result = await response.Content.ReadFromJsonAsync<AddReleaseToPlaylistResult>();
        var page = await ReadPlaylistAsync(manager, playlist);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, result!.Added);
        Assert.Equal(1, result.Skipped);
        Assert.Equal([tracks[1].Id, tracks[0].Id, tracks[2].Id], page.Tracks.Select(t => t.TrackId));
    }

    [Fact]
    public async Task A_Release_the_Member_cannot_see_cannot_be_added()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var listener = await app.LoginAsNewMemberAsync();
        var draft = await CreateCompleteDraftAsync(manager);
        var playlist = await CreatePlaylistAsync(listener, "Stiekem");

        using var response = await listener.PostAsJsonAsync(
            $"/api/playlists/{playlist.Id}/releases", new AddReleaseToPlaylistForm { ReleaseId = draft.Id });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_Member_has_at_most_100_Playlists()
    {
        using var member = await app.LoginAsNewMemberAsync();
        for (var i = 0; i < 100; i++)
        {
            await CreatePlaylistAsync(member, $"Lijst {i}");
        }

        using var response = await member.PostAsJsonAsync("/api/playlists", new PlaylistForm { Name = "Eén te veel" });

        await AssertRefusedAsync(response, HttpStatusCode.Conflict, "at most 100 Playlists");
    }

    [Fact]
    public async Task A_Playlist_has_at_most_1000_Tracks()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await PublishAsync(manager, admin, await CreateArtistAsync(manager), ReleaseType.Single, new DateOnly(2024, 1, 1));
        var track = (await ReadTracksAsync(manager, release))[0];
        var playlist = await CreatePlaylistAsync(manager, "Vol");
        await app.FillPlaylistAsync(playlist.Id, 1000);

        using var one = await AddToPlaylistAsync(manager, playlist, track);
        using var whole = await manager.PostAsJsonAsync(
            $"/api/playlists/{playlist.Id}/releases", new AddReleaseToPlaylistForm { ReleaseId = release.Id });

        await AssertRefusedAsync(one, HttpStatusCode.Conflict, "at most 1,000 Tracks");
        await AssertRefusedAsync(whole, HttpStatusCode.Conflict, "at most 1,000 Tracks");
        Assert.Equal(1000, (await ReadPlaylistAsync(manager, playlist)).Tracks.Count);
    }

    private static async Task<PlaylistSummary> CreatePlaylistAsync(HttpClient member, string name)
    {
        using var response = await member.PostAsJsonAsync("/api/playlists", new PlaylistForm { Name = name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PlaylistSummary>())!;
    }

    private static Task<HttpResponseMessage> AddToPlaylistAsync(HttpClient member, PlaylistSummary playlist, TrackSummary track) =>
        member.PostAsJsonAsync($"/api/playlists/{playlist.Id}/tracks", new AddToPlaylistForm { TrackId = track.Id });

    private static async Task<PlaylistDetails> ReadPlaylistAsync(HttpClient member, PlaylistSummary playlist) =>
        (await member.GetFromJsonAsync<PlaylistDetails>($"/api/playlists/{playlist.Id}/{playlist.Slug}"))!;

    private static async Task<List<PlaylistSummary>> ReadPlaylistsAsync(HttpClient member) =>
        (await member.GetFromJsonAsync<List<PlaylistSummary>>("/api/playlists"))!;

}
