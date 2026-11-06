using System.Net.Http.Headers;
using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

public sealed partial class TrackTests
{
    [Fact]
    public async Task A_managing_Member_s_play_link_plays_the_Draft_Track_from_blob_storage_with_range_requests()
    {
        await app.SetClockAsync(DateTimeOffset.UtcNow);
        using var manager = await app.LoginAsNewMemberAsync();
        var track = await ReadyTrackAsync(manager);

        var link = await RequestPlayLinkAsync(manager, track);

        using var range = new HttpRequestMessage(HttpMethod.Get, link.Url);
        range.Headers.Range = new RangeHeaderValue(0, 99);
        using var played = await BlobStorage.SendAsync(range);
        Assert.Equal(HttpStatusCode.PartialContent, played.StatusCode);
        Assert.Equal("audio/mp4", played.Content.Headers.ContentType?.MediaType);
        Assert.Equal(100, (await played.Content.ReadAsByteArrayAsync()).Length);
    }

    [Fact]
    public async Task The_Admin_gets_a_play_link_for_a_Draft_Track_and_other_Members_are_refused()
    {
        await app.SetClockAsync(DateTimeOffset.UtcNow);
        using var manager = await app.LoginAsNewMemberAsync();
        using var otherMember = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        using var outsider = app.CreateWebClient();
        var track = await ReadyTrackAsync(manager);

        await RequestPlayLinkAsync(admin, track);
        using var refused = await GetPlayLinkAsync(otherMember, track);
        using var notLoggedIn = await GetPlayLinkAsync(outsider, track);

        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, notLoggedIn.StatusCode);
    }

    [Fact]
    public async Task A_Track_that_is_not_Ready_has_no_play_link()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateDraftAsync(manager);
        var failed = await UploadAsync(manager, release, "corrupt.flac");
        await WaitUntilConvertedAsync(manager, release, failed);

        using var refused = await GetPlayLinkAsync(manager, failed);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
    }

    [Fact]
    public async Task A_play_link_dies_with_the_stored_access_policy_and_a_new_link_brings_a_policy_back()
    {
        await app.SetClockAsync(DateTimeOffset.UtcNow);
        using var manager = await app.LoginAsNewMemberAsync();
        var track = await ReadyTrackAsync(manager);
        var before = await RequestPlayLinkAsync(manager, track);
        using var playing = await BlobStorage.GetAsync(before.Url);
        Assert.Equal(HttpStatusCode.OK, playing.StatusCode);

        var service = await app.CreateBlobServiceClientAsync();
        await service.GetBlobContainerClient("audio").SetAccessPolicyAsync(permissions: []);

        using var revoked = await BlobStorage.GetAsync(before.Url);
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);

        var after = await RequestPlayLinkAsync(manager, track);
        using var playingAgain = await BlobStorage.GetAsync(after.Url);
        Assert.Equal(HttpStatusCode.OK, playingAgain.StatusCode);
    }


    [Fact]
    public async Task A_play_link_expires_ten_minutes_after_it_is_issued()
    {
        var now = new DateTimeOffset(DateTime.UtcNow.Date.AddHours(DateTime.UtcNow.Hour), TimeSpan.Zero);
        await app.SetClockAsync(DateTimeOffset.UtcNow);
        using var manager = await app.LoginAsNewMemberAsync();
        var track = await ReadyTrackAsync(manager);
        await app.SetClockAsync(now);

        var link = await RequestPlayLinkAsync(manager, track);
        await app.SetClockAsync(DateTimeOffset.UtcNow);

        // Read from the link itself: Azurite doesn't enforce a link's own expiry when a stored policy signs it.
        var signedExpiry = System.Web.HttpUtility.ParseQueryString(link.Url.Query)["se"];
        Assert.Equal(now.AddMinutes(10), DateTimeOffset.Parse(signedExpiry!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(now.AddMinutes(10), link.ExpiresOn);
    }

    private static async Task<TrackUpload> ReadyTrackAsync(HttpClient manager)
    {
        var release = await CreateDraftAsync(manager);
        var upload = await UploadAsync(manager, release, "tone.flac");
        Assert.Equal(TrackStatus.Ready, (await WaitUntilConvertedAsync(manager, release, upload)).Status);
        return upload;
    }

    private static async Task<PlayLink> RequestPlayLinkAsync(HttpClient member, TrackUpload track)
    {
        using var response = await GetPlayLinkAsync(member, track);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PlayLink>())!;
    }

    private static Task<HttpResponseMessage> GetPlayLinkAsync(HttpClient member, TrackUpload track) =>
        member.GetAsync($"/api/tracks/{track.Track.Id}/play-link");
}
