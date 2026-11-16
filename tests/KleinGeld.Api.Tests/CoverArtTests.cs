using System.Net.Http.Headers;
using System.Net.Http.Json;
using KleinGeld.Contracts;
using SkiaSharp;

namespace KleinGeld.Api.Tests;

[Collection(KleinGeldAppCollection.Name)]
public sealed class CoverArtTests(KleinGeldApp app) : IAsyncLifetime
{
    private static readonly HttpClient Browser = new();

    // Earlier tests leave the clock anywhere; storage checks links against the real time.
    public Task InitializeAsync() => app.SetClockAsync(DateTimeOffset.UtcNow);

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    public async Task A_managing_Member_uploads_a_cover_and_the_Release_shows_its_copies(SKEncodedImageFormat format)
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateReleaseAsync(manager);

        var link = await RequestUploadLinkAsync(manager, release);
        await UploadAsync(link, Image(1200, 1200, format));
        using var confirmed = await ConfirmAsync(manager, release, link);

        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        var cover = (await confirmed.Content.ReadFromJsonAsync<ReleaseDetails>())!.Cover!;
        Assert.Equal(300, await WidthAndHeightAsync(cover.Small));
        Assert.Equal(1000, await WidthAndHeightAsync(cover.Large));
        var read = await manager.GetFromJsonAsync<ReleaseDetails>($"/api/releases/{release.Id}/{release.Slug}");
        Assert.NotNull(read!.Cover);
    }

    // The rows carry only names: xUnit formats theory arguments, which turns a 20 MB byte[] into gigabytes.
    public static TheoryData<string, string> BadCovers => new()
    {
        { "non-square", "square" },
        { "too small", "at least 1000×1000" },
        { "WebP", "JPEG or PNG" },
        { "not an image", "JPEG or PNG" },
        { "over 20 MB", "20 MB" },
    };

    private static byte[] BadCover(string name) => name switch
    {
        "non-square" => Image(1200, 1000),
        "too small" => Image(999, 999),
        "WebP" => Image(1200, 1200, SKEncodedImageFormat.Webp),
        "not an image" => "Dit is geen plaatje"u8.ToArray(),
        "over 20 MB" => [.. Image(1200, 1200), .. new byte[20 * 1024 * 1024]],
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Theory]
    [MemberData(nameof(BadCovers))]
    public async Task A_bad_cover_is_refused_with_the_reason_and_leaves_nothing_behind(string name, string reason)
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateReleaseAsync(manager);

        var link = await RequestUploadLinkAsync(manager, release);
        await UploadAsync(link, BadCover(name));
        using var confirmed = await ConfirmAsync(manager, release, link);

        Assert.True(confirmed.StatusCode == HttpStatusCode.BadRequest, name);
        Assert.Contains(reason, await confirmed.Content.ReadAsStringAsync());
        var read = await manager.GetFromJsonAsync<ReleaseDetails>($"/api/releases/{release.Id}/{release.Slug}");
        Assert.Null(read!.Cover);
        Assert.Empty(await BlobsOfAsync(release));
    }

    [Fact]
    public async Task Confirming_an_upload_that_never_arrived_is_refused()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateReleaseAsync(manager);

        var link = await RequestUploadLinkAsync(manager, release);
        using var confirmed = await ConfirmAsync(manager, release, link);

        Assert.Equal(HttpStatusCode.BadRequest, confirmed.StatusCode);
    }

    [Fact]
    public async Task The_original_and_both_copies_are_stored_in_a_private_container()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateReleaseAsync(manager);
        var file = Image(1000, 1000);

        var cover = await UploadCoverAsync(manager, release, file);

        var blobs = await BlobsOfAsync(release);
        Assert.Equal(["1000.jpg", "300.jpg", "original"], blobs.Select(b => b.Split('/')[^1]).Order(StringComparer.Ordinal));
        var container = (await app.CreateBlobServiceClientAsync()).GetBlobContainerClient("covers");
        var stored = await container.GetBlobClient(blobs.Single(b => b.EndsWith("original"))).DownloadContentAsync();
        Assert.Equal(file, stored.Value.Content.ToArray());
        Assert.Equal("image/png", stored.Value.Details.ContentType);
        Assert.Equal(Azure.Storage.Blobs.Models.PublicAccessType.None, (await container.GetPropertiesAsync()).Value.PublicAccess);

        // Without its SAS signature the same address answers nothing.
        using var anonymous = await Browser.GetAsync(cover.Large.GetLeftPart(UriPartial.Path));
        Assert.False(anonymous.IsSuccessStatusCode);
    }

    [Fact]
    public async Task An_upload_link_is_short_lived_and_cannot_read_or_replace_a_blob()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateReleaseAsync(manager);

        var link = await RequestUploadLinkAsync(manager, release);
        await UploadAsync(link, Image(1000, 1000));

        Assert.InRange(link.ExpiresOn, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30));
        using var read = await Browser.GetAsync(link.Url);
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        using var overwrite = await PutAsync(link, Image(1100, 1100));
        Assert.False(overwrite.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Replacing_a_cover_writes_new_blobs_and_leaves_the_old_ones_untouched()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateReleaseAsync(manager);

        var first = await UploadCoverAsync(manager, release, Image(1000, 1000));
        var firstBytes = await Browser.GetByteArrayAsync(first.Large);
        var second = await UploadCoverAsync(manager, release, Image(1200, 1200));

        Assert.NotEqual(first.Large.AbsolutePath, second.Large.AbsolutePath);
        Assert.Equal(6, (await BlobsOfAsync(release)).Count);
        Assert.Equal(firstBytes, await Browser.GetByteArrayAsync(first.Large));
        var read = await manager.GetFromJsonAsync<ReleaseDetails>($"/api/releases/{release.Id}/{release.Slug}");
        Assert.Equal(second.Large.AbsolutePath, read!.Cover!.Large.AbsolutePath);
    }

    [Fact]
    public async Task Confirming_the_same_upload_twice_changes_nothing()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateReleaseAsync(manager);

        var link = await RequestUploadLinkAsync(manager, release);
        await UploadAsync(link, Image(1000, 1000));
        using var first = await ConfirmAsync(manager, release, link);
        using var second = await ConfirmAsync(manager, release, link);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(3, (await BlobsOfAsync(release)).Count);
    }

    [Fact]
    public async Task Only_managing_Members_get_upload_links_or_confirm_uploads()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var otherMember = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        using var outsider = app.CreateWebClient();
        var release = await CreateReleaseAsync(manager);
        var link = await RequestUploadLinkAsync(manager, release);
        await UploadAsync(link, Image(1000, 1000));

        foreach (var caller in new[] { otherMember, admin })
        {
            using var requested = await caller.PostAsync($"/api/releases/{release.Id}/cover/upload-link", null);
            using var confirmed = await ConfirmAsync(caller, release, link);
            Assert.Equal(HttpStatusCode.NotFound, requested.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, confirmed.StatusCode);
        }
        using var anonymous = await outsider.PostAsync($"/api/releases/{release.Id}/cover/upload-link", null);
        Assert.False(anonymous.IsSuccessStatusCode);
    }

    [Fact]
    public async Task An_upload_for_one_Release_cannot_be_confirmed_on_another()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateReleaseAsync(manager);
        var otherRelease = await CreateReleaseAsync(manager);

        var link = await RequestUploadLinkAsync(manager, release);
        await UploadAsync(link, Image(1000, 1000));
        using var confirmed = await ConfirmAsync(manager, otherRelease, link);

        Assert.Equal(HttpStatusCode.BadRequest, confirmed.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_Release_deletes_its_cover_blobs()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateReleaseAsync(manager);
        await UploadCoverAsync(manager, release, Image(1000, 1000));

        using var deleted = await manager.DeleteAsync($"/api/releases/{release.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await BlobsOfAsync(release));
    }

    private static async Task<CoverArt> UploadCoverAsync(HttpClient manager, ReleaseDetails release, byte[] file)
    {
        var link = await RequestUploadLinkAsync(manager, release);
        await UploadAsync(link, file);
        using var confirmed = await ConfirmAsync(manager, release, link);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        return (await confirmed.Content.ReadFromJsonAsync<ReleaseDetails>())!.Cover!;
    }

    private async Task<List<string>> BlobsOfAsync(ReleaseDetails release)
    {
        var container = (await app.CreateBlobServiceClientAsync()).GetBlobContainerClient("covers");
        var names = new List<string>();
        await foreach (var blob in container.GetBlobsAsync(new Azure.Storage.Blobs.Models.GetBlobsOptions { Prefix = $"{release.Id}/" }))
        {
            names.Add(blob.Name);
        }
        return names;
    }

    private static async Task<ReleaseDetails> CreateReleaseAsync(HttpClient manager)
    {
        using var artistResponse = await manager.PostAsJsonAsync("/api/artists", new ArtistForm { Name = $"De Hoezen {Guid.NewGuid():N}" });
        var artist = await artistResponse.Content.ReadFromJsonAsync<ArtistSummary>();
        using var response = await manager.PostAsJsonAsync($"/api/artists/{artist!.Id}/releases", new ReleaseForm
        {
            Title = "Hoesje",
            Type = ReleaseType.Single,
            ReleaseDate = new DateOnly(2026, 1, 1),
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ReleaseDetails>())!;
    }

    private static async Task<UploadLink> RequestUploadLinkAsync(HttpClient member, ReleaseDetails release)
    {
        using var response = await member.PostAsync($"/api/releases/{release.Id}/cover/upload-link", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<UploadLink>())!;
    }

    private static Task<HttpResponseMessage> ConfirmAsync(HttpClient member, ReleaseDetails release, UploadLink link) =>
        member.PostAsJsonAsync($"/api/releases/{release.Id}/cover", new ConfirmUploadForm { UploadId = link.UploadId });

    /// <summary>What the browser does with an upload link: one PUT straight to blob storage.</summary>
    private static async Task UploadAsync(UploadLink link, byte[] file)
    {
        using var response = await PutAsync(link, file);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static Task<HttpResponseMessage> PutAsync(UploadLink link, byte[] file)
    {
        var content = new ByteArrayContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var request = new HttpRequestMessage(HttpMethod.Put, link.Url) { Content = content };
        request.Headers.Add("x-ms-blob-type", "BlockBlob");
        return Browser.SendAsync(request);
    }

    private static async Task<int> WidthAndHeightAsync(Uri copy)
    {
        using var image = SKBitmap.Decode(await Browser.GetByteArrayAsync(copy));
        Assert.Equal(image.Width, image.Height);
        return image.Width;
    }

    private static byte[] Image(int width, int height, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.Teal);
        using var encoded = bitmap.Encode(format, 90);
        return encoded.ToArray();
    }
}
