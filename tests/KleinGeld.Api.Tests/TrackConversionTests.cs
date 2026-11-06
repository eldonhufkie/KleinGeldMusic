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
