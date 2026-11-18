using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using KleinGeld.Contracts;
using KleinGeld.Data;
using Microsoft.EntityFrameworkCore;

namespace KleinGeld.Worker;

/// <summary>
/// Works through Tracks in Converting, oldest first. That status is the whole work state: a Track that was being
/// converted when the worker stopped is simply converted again, so only one worker may run. Nothing is locked while
/// FFmpeg runs, so a Member can remove the Track meanwhile; the result is then thrown away.
/// </summary>
internal sealed class ConversionWorker(
    IServiceScopeFactory scopes, BlobServiceClient blobs, AudioConverter converter, ILogger<ConversionWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var worked = false;
            try
            {
                worked = await ConvertNextAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Storage, the database or FFmpeg itself is unavailable, which says nothing about the file: try again.
                logger.LogError(e, "Converting a Track stopped; trying again.");
            }

            if (!worked)
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }

    private async Task<bool> ConvertNextAsync(CancellationToken cancellation)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KleinGeldDbContext>();

        var next = await db.Tracks
            .Where(t => t.Status == TrackStatus.Converting)
            .OrderBy(t => t.Id)
            .Select(t => new { t.Id, t.OriginalBlobName })
            .FirstOrDefaultAsync(cancellation);
        if (next is null)
        {
            return false;
        }

        var audio = blobs.GetBlobContainerClient(Track.AudioContainer);
        var folder = Directory.CreateTempSubdirectory("kleingeld-track-");
        try
        {
            var original = Path.Combine(folder.FullName, "original");
            var converted = Path.Combine(folder.FullName, "converted.m4a");
            var duration = await TryDownloadAsync(audio.GetBlobClient(next.OriginalBlobName), original, cancellation)
                ? await converter.ConvertAsync(original, converted, cancellation)
                : null;

            var unchanged = db.Tracks.Where(t =>
                t.Id == next.Id && t.Status == TrackStatus.Converting && t.OriginalBlobName == next.OriginalBlobName);
            if (duration is null)
            {
                logger.LogWarning("Track {TrackId} could not be converted.", next.Id);
                await unchanged.ExecuteUpdateAsync(track => track.SetProperty(t => t.Status, TrackStatus.Failed), cancellation);
                return true;
            }

            var blobName = $"tracks/{next.Id}/{Guid.CreateVersion7()}.m4a";
            var blob = audio.GetBlobClient(blobName);
            await blob.UploadAsync(converted, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "audio/mp4" },
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
            }, cancellation);

            var updated = await unchanged
                .ExecuteUpdateAsync(track => track
                    .SetProperty(t => t.Status, TrackStatus.Ready)
                    .SetProperty(t => t.ConvertedBlobName, blobName)
                    .SetProperty(t => t.Duration, duration), cancellation);
            if (updated == 0)
            {
                await blob.DeleteIfExistsAsync(cancellationToken: cancellation);
            }
        }
        finally
        {
            folder.Delete(recursive: true);
        }
        return true;
    }

    /// <summary>A missing original can never convert, so unlike an outage it must not be retried for ever.</summary>
    private static async Task<bool> TryDownloadAsync(BlobClient blob, string path, CancellationToken cancellation)
    {
        try
        {
            await blob.DownloadToAsync(path, cancellation);
            return true;
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            return false;
        }
    }
}
