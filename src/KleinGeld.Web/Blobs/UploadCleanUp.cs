using Azure.Storage.Blobs;
using KleinGeld.Data;
using KleinGeld.Web.Api;
using Microsoft.EntityFrameworkCore;

namespace KleinGeld.Web.Blobs;

/// <summary>Deletes uploaded blobs that never got attached to anything.</summary>
internal sealed class UploadCleanUp(BlobServiceClient blobs, IServiceScopeFactory scopes, TimeProvider clock)
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromHours(24);

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var createdBefore = clock.GetUtcNow() - GracePeriod;
        var covers = await OlderBlobsAsync(CoverStore.Container, createdBefore, cancellationToken);
        var audio = await OlderBlobsAsync(TrackEndpoints.AudioContainer, createdBefore, cancellationToken);

        // Read after listing, so an upload attached in the meantime is seen as attached.
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KleinGeldDbContext>();
        var coverIds = (await db.Releases
            .Where(r => r.CoverId != null)
            .Select(r => r.CoverId!.Value)
            .ToListAsync(cancellationToken)).ToHashSet();
        var trackIds = (await db.Tracks.Select(t => t.Id).ToListAsync(cancellationToken)).ToHashSet();

        await DeleteAsync(CoverStore.Container, covers.Where(name => IsOrphan(name, coverIds)), cancellationToken);
        await DeleteAsync(TrackEndpoints.AudioContainer, audio.Where(name => IsOrphan(name, trackIds)), cancellationToken);
    }

    /// <summary>
    /// Names are <c>{releaseId}/{coverId}/...</c> and <c>tracks/{trackId}/...</c>: the second part says who owns the
    /// blob. A name of any other shape isn't one this job understands, so it stays.
    /// </summary>
    private static bool IsOrphan(string name, HashSet<Guid> owners)
    {
        var parts = name.Split('/');
        return parts.Length >= 3 && Guid.TryParse(parts[1], out var owner) && !owners.Contains(owner);
    }

    private async Task<List<string>> OlderBlobsAsync(string container, DateTimeOffset createdBefore, CancellationToken cancellationToken)
    {
        var names = new List<string>();
        var client = blobs.GetBlobContainerClient(container);
        if (!await client.ExistsAsync(cancellationToken))
        {
            return names;
        }

        await foreach (var blob in client.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            if (blob.Properties.CreatedOn < createdBefore)
            {
                names.Add(blob.Name);
            }
        }
        return names;
    }

    private async Task DeleteAsync(string container, IEnumerable<string> names, CancellationToken cancellationToken)
    {
        var client = blobs.GetBlobContainerClient(container);
        foreach (var name in names)
        {
            await client.DeleteBlobIfExistsAsync(name, cancellationToken: cancellationToken);
        }
    }
}

/// <summary>Runs the clean-up at start and then daily; starting counts because the host may restart more often.</summary>
internal sealed class DailyUploadCleanUp(UploadCleanUp cleanUp, TimeProvider clock, ILogger<DailyUploadCleanUp> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1), clock);
        do
        {
            try
            {
                await cleanUp.RunAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Orphans can wait for tomorrow; an exception leaving here would stop the whole host.
                logger.LogError(e, "Cleaning up unattached uploads failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
