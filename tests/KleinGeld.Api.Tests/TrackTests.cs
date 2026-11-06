using System.Net.Http.Headers;
using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

[Collection(KleinGeldAppCollection.Name)]
public sealed partial class TrackTests(KleinGeldApp app)
{
    private static readonly HttpClient BlobStorage = new();

    [Theory]
    [InlineData("tone.flac")]
    [InlineData("tone.wav")]
    [InlineData("tone.aiff")]
    [InlineData("tone-alac.m4a")]
    [InlineData("tone.mp3")]
    [InlineData("tone-aac.m4a")]
    public async Task An_audio_file_uploads_straight_to_blob_storage_and_its_Track_moves_on_from_Uploading(string fixture)
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateDraftAsync(manager);
        var file = await ReadFixtureAsync(fixture);

        var upload = await RequestUploadAsync(manager, release, fixture, file.Length);

        Assert.Equal(TrackStatus.Uploading, upload.Track.Status);
        Assert.Equal(Path.GetFileNameWithoutExtension(fixture), upload.Track.Title);
        Assert.Equal([upload.Track], await ListTracksAsync(manager, release));

        await PutBlobAsync(upload.UploadLink, file);
        using var confirmed = await ConfirmAsync(manager, release, upload);

        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        // The worker may already have finished: all that matters here is that the upload was accepted.
        Assert.NotEqual(TrackStatus.Uploading, Assert.Single(await ListTracksAsync(manager, release)).Status);
    }

    [Fact]
    public async Task A_file_is_judged_by_its_content_so_audio_with_any_name_is_accepted()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateDraftAsync(manager);
        var file = await ReadFixtureAsync("tone.mp3");
        var upload = await RequestUploadAsync(manager, release, "liner notes.txt", file.Length);

        await PutBlobAsync(upload.UploadLink, file);
        using var confirmed = await ConfirmAsync(manager, release, upload);

        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
    }

    [Theory]
    [InlineData("not-audio.flac")]
    [InlineData("tone.ogg")]
    [InlineData("video.mp4")]
    public async Task A_file_that_is_not_accepted_audio_is_rejected_with_a_message_and_leaves_nothing_behind(string fixture)
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateDraftAsync(manager);
        var file = await ReadFixtureAsync(fixture);
        var upload = await RequestUploadAsync(manager, release, fixture, file.Length);
        await PutBlobAsync(upload.UploadLink, file);

        using var confirmed = await ConfirmAsync(manager, release, upload);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, confirmed.StatusCode);
        Assert.Contains("FLAC, WAV, AIFF, ALAC, MP3 or AAC", await confirmed.Content.ReadAsStringAsync());
        Assert.Empty(await ListTracksAsync(manager, release));
        Assert.Empty(await BlobsOfAsync(upload));
    }

    [Fact]
    public async Task A_file_declared_over_500_MB_gets_no_upload_link()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateDraftAsync(manager);

        using var response = await PostUploadAsync(manager, release, "huge.wav", 500L * 1024 * 1024 + 1);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await ListTracksAsync(manager, release));
    }

    [Fact]
    public async Task A_file_that_turns_out_over_500_MB_is_rejected_whatever_size_was_declared()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateDraftAsync(manager);
        var upload = await RequestUploadAsync(manager, release, "small.wav", 1000);
        await PutBlobAsync(upload.UploadLink, new byte[500 * 1024 * 1024 + 1]);

        using var confirmed = await ConfirmAsync(manager, release, upload);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, confirmed.StatusCode);
        Assert.Contains("500 MB", await confirmed.Content.ReadAsStringAsync());
        Assert.Empty(await ListTracksAsync(manager, release));
    }

    [Fact]
    public async Task A_Track_stays_Uploading_until_its_file_has_arrived()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateDraftAsync(manager);
        var upload = await RequestUploadAsync(manager, release, "tone.wav", 1000);

        using var confirmed = await ConfirmAsync(manager, release, upload);

        Assert.Equal(HttpStatusCode.Conflict, confirmed.StatusCode);
        Assert.Equal(TrackStatus.Uploading, Assert.Single(await ListTracksAsync(manager, release)).Status);
    }

    [Fact]
    public async Task A_Release_holds_50_Tracks_even_when_they_are_added_at_once_and_the_51st_is_refused_with_a_message()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateDraftAsync(manager);

        var responses = await Task.WhenAll(Enumerable.Range(1, 52).Select(n => PostUploadAsync(manager, release, $"{n}.flac", 1000)));

        Assert.Equal(50, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        var refused = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToList();
        Assert.Equal(2, refused.Count);
        Assert.Contains("at most 50 Tracks", await refused[0].Content.ReadAsStringAsync());
        Assert.Equal(Enumerable.Range(1, 50), (await ListTracksAsync(manager, release)).Select(t => t.Position));
    }

    [Fact]
    public async Task Removing_a_Track_from_a_Draft_deletes_its_blob_at_once_and_closes_the_gap_in_the_numbers()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateDraftAsync(manager);
        var file = await ReadFixtureAsync("tone.wav");
        var first = await RequestUploadAsync(manager, release, "een.wav", file.Length);
        var second = await RequestUploadAsync(manager, release, "twee.wav", file.Length);
        await PutBlobAsync(first.UploadLink, file);
        Assert.Single(await BlobsOfAsync(first));

        using var removed = await manager.DeleteAsync($"/api/releases/{release.Id}/tracks/{first.Track.Id}");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Empty(await BlobsOfAsync(first));
        Assert.Equal([second.Track with { Position = 1 }], await ListTracksAsync(manager, release));
    }

    [Fact]
    public async Task Deleting_a_Release_deletes_its_Tracks_blobs()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateDraftAsync(manager);
        var file = await ReadFixtureAsync("tone.wav");
        var upload = await RequestUploadAsync(manager, release, "een.wav", file.Length);
        await PutBlobAsync(upload.UploadLink, file);

        using var deleted = await manager.DeleteAsync($"/api/releases/{release.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await BlobsOfAsync(upload));
    }

    [Fact]
    public async Task An_upload_link_writes_once_and_never_overwrites()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateDraftAsync(manager);
        var file = await ReadFixtureAsync("tone.wav");
        var upload = await RequestUploadAsync(manager, release, "een.wav", file.Length);
        await PutBlobAsync(upload.UploadLink, file);

        using var again = await SendBlobAsync(upload.UploadLink, [1, 2, 3]);

        Assert.False(again.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Only_managing_Members_add_confirm_or_remove_Tracks_and_a_Draft_s_Tracks_stay_hidden()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var otherMember = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateDraftAsync(manager);
        var upload = await RequestUploadAsync(manager, release, "een.wav", 1000);

        using var added = await PostUploadAsync(otherMember, release, "van mij.wav", 1000);
        using var confirmed = await ConfirmAsync(otherMember, release, upload);
        using var removed = await otherMember.DeleteAsync($"/api/releases/{release.Id}/tracks/{upload.Track.Id}");
        using var listed = await otherMember.GetAsync($"/api/tracks?releaseId={release.Id}");

        Assert.Equal(HttpStatusCode.NotFound, added.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, confirmed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, listed.StatusCode);
        Assert.Equal([upload.Track], await ListTracksAsync(admin, release));
    }

    /// <summary>The blobs stored for a Track, as blob storage itself reports them.</summary>
    private async Task<List<string>> BlobsOfAsync(TrackUpload upload)
    {
        var blobs = await app.CreateBlobServiceClientAsync();
        var names = new List<string>();
        await foreach (var blob in blobs.GetBlobContainerClient("audio").GetBlobsAsync(new() { Prefix = $"tracks/{upload.Track.Id}/" }))
        {
            names.Add(blob.Name);
        }
        return names;
    }

    private static Task<byte[]> ReadFixtureAsync(string name) =>
        File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static async Task<ReleaseDetails> CreateDraftAsync(HttpClient manager)
    {
        using var artistResponse = await manager.PostAsJsonAsync("/api/artists", new ArtistForm { Name = "De Testers" });
        var artist = await artistResponse.Content.ReadFromJsonAsync<ArtistSummary>();
        using var response = await manager.PostAsJsonAsync($"/api/artists/{artist!.Id}/releases", new ReleaseForm
        {
            Title = "Eerste Plaat",
            Type = ReleaseType.Album,
            ReleaseDate = new DateOnly(2026, 1, 1),
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ReleaseDetails>())!;
    }

    private static async Task<TrackUpload> RequestUploadAsync(HttpClient manager, ReleaseDetails release, string fileName, long size)
    {
        using var response = await PostUploadAsync(manager, release, fileName, size);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TrackUpload>())!;
    }

    private static Task<HttpResponseMessage> PostUploadAsync(HttpClient member, ReleaseDetails release, string fileName, long size) =>
        member.PostAsJsonAsync($"/api/releases/{release.Id}/tracks", new TrackUploadForm { FileName = fileName, Size = size });

    private static Task<HttpResponseMessage> ConfirmAsync(HttpClient member, ReleaseDetails release, TrackUpload upload) =>
        member.PostAsync($"/api/releases/{release.Id}/tracks/{upload.Track.Id}/confirm", null);

    private static async Task<List<TrackSummary>> ListTracksAsync(HttpClient member, ReleaseDetails release) =>
        (await member.GetFromJsonAsync<List<TrackSummary>>($"/api/tracks?releaseId={release.Id}"))!;

    /// <summary>Uploads the way the browser does: one PUT to the link, no cookie.</summary>
    private static async Task PutBlobAsync(Uri link, byte[] file)
    {
        using var response = await SendBlobAsync(link, file);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static Task<HttpResponseMessage> SendBlobAsync(Uri link, byte[] file)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, link) { Content = new ByteArrayContent(file) };
        request.Headers.Add("x-ms-blob-type", "BlockBlob");
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return BlobStorage.SendAsync(request);
    }
}
