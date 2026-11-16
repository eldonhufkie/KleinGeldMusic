using System.Net.Http.Headers;
using System.Net.Http.Json;
using KleinGeld.Contracts;
using SkiaSharp;

namespace KleinGeld.Api.Tests;

[Collection(KleinGeldAppCollection.Name)]
public sealed class UploadCleanUpTests(KleinGeldApp app) : IAsyncLifetime
{
    private static readonly HttpClient Browser = new();

    private DateTimeOffset _uploadedAt;

    // Blob storage dates blobs and checks links by the real time, so uploads happen with the clock on it.
    public async Task InitializeAsync()
    {
        _uploadedAt = DateTimeOffset.UtcNow;
        await app.SetClockAsync(_uploadedAt);
    }

    public Task DisposeAsync() => app.SetClockAsync(DateTimeOffset.UtcNow);

    [Fact]
    public async Task A_cover_upload_nobody_confirmed_is_gone_after_24_hours()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateReleaseAsync(manager);
        await UploadCoverAsync(manager, release);

        await app.SetClockAsync(_uploadedAt.AddHours(25));
        await app.RunUploadCleanUpAsync();

        Assert.Empty(await BlobsAsync("covers", $"{release.Id}/"));
    }

    [Fact]
    public async Task A_cover_set_on_a_Release_stays_however_old_it_gets()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateReleaseAsync(manager);
        var link = await RequestCoverLinkAsync(manager, release);
        await PutBlobAsync(link.Url, SquareCover());
        using var confirmed = await manager.PostAsJsonAsync(
            $"/api/releases/{release.Id}/cover", new ConfirmUploadForm { UploadId = link.UploadId });
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        var stored = await BlobsAsync("covers", $"{release.Id}/");

        await app.SetClockAsync(_uploadedAt.AddHours(25));
        await app.RunUploadCleanUpAsync();

        Assert.Equal(stored, await BlobsAsync("covers", $"{release.Id}/"));
    }

    [Fact]
    public async Task An_unattached_upload_gets_the_full_24_hours()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateReleaseAsync(manager);
        await UploadCoverAsync(manager, release);

        await app.SetClockAsync(_uploadedAt.AddHours(23));
        await app.RunUploadCleanUpAsync();

        Assert.Single(await BlobsAsync("covers", $"{release.Id}/"));
    }

    [Fact]
    public async Task Audio_that_arrives_after_its_Track_was_removed_is_gone_after_24_hours()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateReleaseAsync(manager);
        var upload = await RequestTrackAsync(manager, release);
        using var removed = await manager.DeleteAsync($"/api/releases/{release.Id}/tracks/{upload.Track.Id}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        await PutBlobAsync(upload.UploadLink, "te laat"u8.ToArray());

        await app.SetClockAsync(_uploadedAt.AddHours(25));
        await app.RunUploadCleanUpAsync();

        Assert.Empty(await BlobsAsync("audio", $"tracks/{upload.Track.Id}/"));
    }

    [Fact]
    public async Task The_audio_of_a_Track_stays_however_old_it_gets()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateReleaseAsync(manager);
        var upload = await RequestTrackAsync(manager, release);
        await PutBlobAsync(upload.UploadLink, "nog niet bevestigd"u8.ToArray());

        await app.SetClockAsync(_uploadedAt.AddHours(25));
        await app.RunUploadCleanUpAsync();

        Assert.Single(await BlobsAsync("audio", $"tracks/{upload.Track.Id}/"));
    }

    private async Task<List<string>> BlobsAsync(string container, string prefix)
    {
        var blobs = await app.CreateBlobServiceClientAsync();
        var names = new List<string>();
        await foreach (var blob in blobs.GetBlobContainerClient(container).GetBlobsAsync(new() { Prefix = prefix }))
        {
            names.Add(blob.Name);
        }
        return names;
    }

    private static async Task<ReleaseDetails> CreateReleaseAsync(HttpClient manager)
    {
        using var artistResponse = await manager.PostAsJsonAsync("/api/artists", new ArtistForm { Name = $"De Opruimers {Guid.NewGuid():N}" });
        var artist = await artistResponse.Content.ReadFromJsonAsync<ArtistSummary>();
        using var response = await manager.PostAsJsonAsync($"/api/artists/{artist!.Id}/releases", new ReleaseForm
        {
            Title = "Grote Schoonmaak",
            Type = ReleaseType.Album,
            ReleaseDate = new DateOnly(2026, 1, 1),
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ReleaseDetails>())!;
    }

    private static async Task UploadCoverAsync(HttpClient manager, ReleaseDetails release)
    {
        var link = await RequestCoverLinkAsync(manager, release);
        await PutBlobAsync(link.Url, "nog niet bekeken"u8.ToArray());
    }

    private static async Task<UploadLink> RequestCoverLinkAsync(HttpClient manager, ReleaseDetails release)
    {
        using var response = await manager.PostAsync($"/api/releases/{release.Id}/cover/upload-link", null);
        return (await response.Content.ReadFromJsonAsync<UploadLink>())!;
    }

    private static async Task<TrackUpload> RequestTrackAsync(HttpClient manager, ReleaseDetails release)
    {
        using var response = await manager.PostAsJsonAsync(
            $"/api/releases/{release.Id}/tracks", new TrackUploadForm { FileName = "tone.mp3", Size = 1000 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TrackUpload>())!;
    }

    private static byte[] SquareCover()
    {
        using var bitmap = new SKBitmap(1000, 1000);
        bitmap.Erase(SKColors.Teal);
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 90);
        return encoded.ToArray();
    }

    /// <summary>Uploads the way the browser does: one PUT to the link, no cookie.</summary>
    private static async Task PutBlobAsync(Uri link, byte[] file)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, link) { Content = new ByteArrayContent(file) };
        request.Headers.Add("x-ms-blob-type", "BlockBlob");
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await Browser.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
