using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

public sealed partial class TrackTests
{
    private static readonly TimeSpan ConversionTimeout = TimeSpan.FromMinutes(1);

    [Theory]
    [InlineData("tone.flac")]
    [InlineData("tone.wav")]
    [InlineData("tone.aiff")]
    [InlineData("tone-alac.m4a")]
    [InlineData("tone.mp3")]
    [InlineData("tone-aac.m4a")]
    public async Task An_uploaded_Track_becomes_Ready_with_its_duration(string fixture)
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateDraftAsync(manager);

        var upload = await UploadAsync(manager, release, fixture);

        var track = await WaitUntilConvertedAsync(manager, release, upload);
        Assert.Equal(TrackStatus.Ready, track.Status);
        // The fixtures are half a second of tone; encoders pad a little.
        Assert.InRange(track.Duration!.Value.TotalSeconds, 0.45, 0.65);
    }

    [Fact]
    public async Task A_corrupt_file_ends_up_Failed_and_its_original_is_kept()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateDraftAsync(manager);

        var upload = await UploadAsync(manager, release, "corrupt.flac");

        var track = await WaitUntilConvertedAsync(manager, release, upload);
        Assert.Equal(TrackStatus.Failed, track.Status);
        Assert.Null(track.Duration);
        Assert.Equal([OriginalBlobOf(upload)], await BlobsOfAsync(upload));
    }

    [Fact]
    public async Task A_converted_Track_is_a_new_fast_start_AAC_blob_beside_its_original()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateDraftAsync(manager);

        var upload = await UploadAsync(manager, release, "tone.flac");
        await WaitUntilConvertedAsync(manager, release, upload);

        var blobs = await BlobsOfAsync(upload);
        Assert.Equal(2, blobs.Count);
        Assert.Contains(OriginalBlobOf(upload), blobs);
        var converted = Assert.Single(blobs, name => name.EndsWith(".m4a", StringComparison.Ordinal));

        var service = await app.CreateBlobServiceClientAsync();
        var content = (await service.GetBlobContainerClient("audio").GetBlobClient(converted).DownloadContentAsync()).Value.Content.ToArray();
        var text = System.Text.Encoding.Latin1.GetString(content);
        Assert.Contains("mp4a", text);
        Assert.True(text.IndexOf("moov", StringComparison.Ordinal) < text.IndexOf("mdat", StringComparison.Ordinal), "moov must come first");
    }

    [Fact]
    public async Task Removing_a_converted_Track_deletes_its_converted_blob_too()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateDraftAsync(manager);
        var upload = await UploadAsync(manager, release, "tone.flac");
        await WaitUntilConvertedAsync(manager, release, upload);

        using var removed = await manager.DeleteAsync($"/api/releases/{release.Id}/tracks/{upload.Track.Id}");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Empty(await BlobsOfAsync(upload));
    }

    [Fact]
    public async Task Deleting_a_Release_deletes_its_converted_blobs_too()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateDraftAsync(manager);
        var upload = await UploadAsync(manager, release, "tone.flac");
        await WaitUntilConvertedAsync(manager, release, upload);

        using var deleted = await manager.DeleteAsync($"/api/releases/{release.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await BlobsOfAsync(upload));
    }

    [Fact]
    public async Task A_Failed_Track_can_be_removed()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateDraftAsync(manager);
        var upload = await UploadAsync(manager, release, "corrupt.flac");
        await WaitUntilConvertedAsync(manager, release, upload);

        using var removed = await manager.DeleteAsync($"/api/releases/{release.Id}/tracks/{upload.Track.Id}");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Empty(await ListTracksAsync(manager, release));
        Assert.Empty(await BlobsOfAsync(upload));
    }

    [Fact]
    public async Task Replacing_a_Failed_Track_s_audio_keeps_the_Track_writes_new_blobs_and_converts_again()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateDraftAsync(manager);
        var failed = await UploadAsync(manager, release, "corrupt.flac");
        await WaitUntilConvertedAsync(manager, release, failed);
        var file = await ReadFixtureAsync("tone.flac");

        var replacement = await RequestReplacementAsync(manager, release, failed, file.Length);
        await PutBlobAsync(replacement.UploadLink, file);
        using var confirmed = await ConfirmAsync(manager, release, replacement);

        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal(failed.Track.Id, replacement.Track.Id);
        Assert.NotEqual(OriginalBlobOf(failed), OriginalBlobOf(replacement));
        var track = await WaitUntilConvertedAsync(manager, release, replacement);
        Assert.Equal(TrackStatus.Ready, track.Status);
        Assert.Equal(failed.Track.Title, track.Title);
        var blobs = await BlobsOfAsync(failed);
        Assert.Equal(2, blobs.Count);
        Assert.DoesNotContain(OriginalBlobOf(failed), blobs);
    }

    [Fact]
    public async Task A_replacement_that_is_not_accepted_audio_leaves_the_Track_Failed()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateDraftAsync(manager);
        var failed = await UploadAsync(manager, release, "corrupt.flac");
        await WaitUntilConvertedAsync(manager, release, failed);
        var file = await ReadFixtureAsync("not-audio.flac");

        var replacement = await RequestReplacementAsync(manager, release, failed, file.Length);
        await PutBlobAsync(replacement.UploadLink, file);
        using var confirmed = await ConfirmAsync(manager, release, replacement);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, confirmed.StatusCode);
        Assert.Equal(TrackStatus.Failed, Assert.Single(await ListTracksAsync(manager, release)).Status);
    }

    [Fact]
    public async Task Only_a_Failed_Track_can_be_replaced_and_only_by_a_managing_Member()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var otherMember = await app.LoginAsNewMemberAsync();
        var release = await CreateDraftAsync(manager);
        var ready = await UploadAsync(manager, release, "tone.flac");
        var failed = await UploadAsync(manager, release, "corrupt.flac");
        await WaitUntilConvertedAsync(manager, release, ready);
        await WaitUntilConvertedAsync(manager, release, failed);

        using var notFailed = await PostReplacementAsync(manager, release, ready, 1000);
        using var notManager = await PostReplacementAsync(otherMember, release, failed, 1000);

        Assert.Equal(HttpStatusCode.Conflict, notFailed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, notManager.StatusCode);
    }

    private static async Task<TrackUpload> RequestReplacementAsync(HttpClient manager, ReleaseDetails release, TrackUpload track, long size)
    {
        using var response = await PostReplacementAsync(manager, release, track, size);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TrackUpload>())!;
    }

    private static Task<HttpResponseMessage> PostReplacementAsync(HttpClient member, ReleaseDetails release, TrackUpload track, long size) =>
        member.PostAsJsonAsync(
            $"/api/releases/{release.Id}/tracks/{track.Track.Id}/replace",
            new TrackUploadForm { FileName = "vervanging.flac", Size = size });

    /// <summary>The upload link's path is the container, then the blob name.</summary>
    private static string OriginalBlobOf(TrackUpload upload)
    {
        var path = Uri.UnescapeDataString(upload.UploadLink.AbsolutePath);
        return path[(path.IndexOf("/audio/", StringComparison.Ordinal) + "/audio/".Length)..];
    }

    private static async Task<TrackUpload> UploadAsync(HttpClient manager, ReleaseDetails release, string fixture)
    {
        var file = await ReadFixtureAsync(fixture);
        var upload = await RequestUploadAsync(manager, release, fixture, file.Length);
        await PutBlobAsync(upload.UploadLink, file);
        using var confirmed = await ConfirmAsync(manager, release, upload);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        return upload;
    }

    /// <summary>Polls as the Track list in the browser does, until the worker has finished with the Track.</summary>
    private static async Task<TrackSummary> WaitUntilConvertedAsync(HttpClient manager, ReleaseDetails release, TrackUpload upload)
    {
        using var timeout = new CancellationTokenSource(ConversionTimeout);
        while (true)
        {
            var track = (await ListTracksAsync(manager, release)).Single(t => t.Id == upload.Track.Id);
            if (track.Status is TrackStatus.Ready or TrackStatus.Failed)
            {
                return track;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(200), timeout.Token);
        }
    }
}
