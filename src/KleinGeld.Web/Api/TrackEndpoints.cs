using System.Security.Claims;
using Azure;
using KleinGeld.Contracts;
using KleinGeld.Data;
using KleinGeld.Web.Blobs;
using Microsoft.EntityFrameworkCore;

namespace KleinGeld.Web.Api;

internal static class TrackEndpoints
{
    public const string AudioContainer = Track.AudioContainer;

    /// <summary>Endpoints for adding audio to a Release: request a link, upload to it, confirm; and for removing it.</summary>
    public static void MapTrackEndpoints(this RouteGroupBuilder api)
    {
        // Not under /releases/{id}: a GET there is the Release itself, whose slug could be "tracks".
        api.MapGet("/tracks", async (Guid releaseId, ClaimsPrincipal caller, KleinGeldDbContext db) =>
        {
            var release = await db.Releases
                .Include(r => r.Tracks)
                .Where(r => r.Id == releaseId)
                .VisibleTo(caller)
                .SingleOrDefaultAsync();

            return release is null
                ? Results.NotFound()
                : Results.Ok(release.Tracks.OrderBy(t => t.Position).Select(ToSummary));
        });

        api.MapPost("/releases/{id:guid}/tracks", async (Guid id, TrackUploadForm form, ClaimsPrincipal caller, KleinGeldDbContext db, BlobLinks links) =>
        {
            Track? added = null;
            var refused = await WithReleaseLockedAsync(db, id, caller, async release =>
            {
                if (release.Tracks.Count >= TrackUploadForm.MaxTracksPerRelease)
                {
                    return Results.Problem(
                        $"A Release can hold at most {TrackUploadForm.MaxTracksPerRelease} Tracks.",
                        statusCode: StatusCodes.Status409Conflict);
                }

                var trackId = Guid.CreateVersion7();
                added = new Track
                {
                    Id = trackId,
                    ReleaseId = release.Id,
                    Title = TitleFrom(form.FileName),
                    Position = release.Tracks.Count + 1,
                    // No extension: the name must tell ffprobe nothing about the file.
                    OriginalBlobName = $"tracks/{trackId}/{Guid.CreateVersion7()}",
                };
                db.Tracks.Add(added);
                await db.SaveChangesAsync();
                return null;
            });
            if (refused is not null)
            {
                return refused;
            }

            var link = await links.ForUploadAsync(AudioContainer, added!.OriginalBlobName, added.Id);
            return Results.Created($"/api/tracks?releaseId={id}", new TrackUpload(ToSummary(added), link.Url));
        });

        api.MapPost("/releases/{id:guid}/tracks/{trackId:guid}/confirm", async (
            Guid id, Guid trackId, ClaimsPrincipal caller, KleinGeldDbContext db, BlobLinks links, AudioProbe probe) =>
        {
            var memberId = caller.MemberId();
            var track = await db.Tracks
                .Where(t => t.Id == trackId && t.ReleaseId == id)
                .Where(t => t.Release.Artist.Managers.Any(m => m.Id == memberId))
                .SingleOrDefaultAsync();
            if (track is null)
            {
                return Results.NotFound();
            }
            if (track.Status != TrackStatus.Uploading)
            {
                return Results.Ok(ToSummary(track));
            }

            long size;
            try
            {
                size = (await links.Blob(AudioContainer, track.OriginalBlobName).GetPropertiesAsync()).Value.ContentLength;
            }
            catch (RequestFailedException e) when (e.Status == StatusCodes.Status404NotFound)
            {
                return Results.Problem("The file hasn't finished uploading.", statusCode: StatusCodes.Status409Conflict);
            }

            // The size the browser declared proves nothing, and a SAS link can't limit what is uploaded to it.
            var problem = size > TrackUploadForm.MaxFileSize
                ? "A Track's file can be at most 500 MB."
                : await probe.IsAcceptedAudioAsync(links.ForServerReading(AudioContainer, track.OriginalBlobName))
                    ? null
                    : "That file isn't audio we accept. Use FLAC, WAV, AIFF, ALAC, MP3 or AAC.";
            if (problem is not null)
            {
                if (track.IsReplacingFailedAudio)
                {
                    track.Status = TrackStatus.Failed;
                    await db.SaveChangesAsync();
                    await links.Blob(AudioContainer, track.OriginalBlobName).DeleteIfExistsAsync();
                }
                else
                {
                    await RemoveAsync(db, links, id, trackId, caller);
                }
                return Results.Problem(problem, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            track.Status = TrackStatus.Converting;
            track.IsReplacingFailedAudio = false;
            await db.SaveChangesAsync();
            return Results.Ok(ToSummary(track));
        });

        // Keeps the Track and its place; the new file then goes through the same upload and confirm as a new one.
        api.MapPost("/releases/{id:guid}/tracks/{trackId:guid}/replace", async (
            Guid id, Guid trackId, TrackUploadForm form, ClaimsPrincipal caller, KleinGeldDbContext db, BlobLinks links) =>
        {
            Track? replaced = null;
            List<string> oldBlobNames = [];
            var refused = await WithReleaseLockedAsync(db, id, caller, async release =>
            {
                var track = release.Tracks.SingleOrDefault(t => t.Id == trackId);
                if (track is null)
                {
                    return Results.NotFound();
                }
                if (track.Status != TrackStatus.Failed)
                {
                    return Results.Problem("Only a Failed Track's audio can be replaced.", statusCode: StatusCodes.Status409Conflict);
                }

                oldBlobNames = track.BlobNames().ToList();
                track.OriginalBlobName = $"tracks/{trackId}/{Guid.CreateVersion7()}";
                track.ConvertedBlobName = null;
                track.Duration = null;
                track.Status = TrackStatus.Uploading;
                track.IsReplacingFailedAudio = true;
                await db.SaveChangesAsync();
                replaced = track;
                return null;
            });
            if (refused is not null)
            {
                return refused;
            }

            foreach (var blobName in oldBlobNames)
            {
                await links.Blob(AudioContainer, blobName).DeleteIfExistsAsync();
            }
            var link = await links.ForUploadAsync(AudioContainer, replaced!.OriginalBlobName, replaced.Id);
            return Results.Ok(new TrackUpload(ToSummary(replaced), link.Url));
        });

        api.MapDelete("/releases/{id:guid}/tracks/{trackId:guid}", (
            Guid id, Guid trackId, ClaimsPrincipal caller, KleinGeldDbContext db, BlobLinks links) =>
            RemoveAsync(db, links, id, trackId, caller));
    }

    /// <summary>
    /// Takes a Track off its Release, closes the gap in the Track numbers and deletes its blob at once. The database
    /// goes first: a blob left behind by a failure is an orphan the daily clean-up finds, a Track without audio isn't.
    /// </summary>
    private static async Task<IResult> RemoveAsync(KleinGeldDbContext db, BlobLinks links, Guid id, Guid trackId, ClaimsPrincipal caller)
    {
        List<string> blobNames = [];
        var refused = await WithReleaseLockedAsync(db, id, caller, async release =>
        {
            var track = release.Tracks.SingleOrDefault(t => t.Id == trackId);
            if (track is null)
            {
                return Results.NotFound();
            }

            blobNames = track.BlobNames().ToList();
            db.Tracks.Remove(track);
            foreach (var later in release.Tracks.Where(t => t.Position > track.Position))
            {
                later.Position--;
            }
            await db.SaveChangesAsync();
            return null;
        });
        if (refused is not null)
        {
            return refused;
        }

        foreach (var blobName in blobNames)
        {
            await links.Blob(AudioContainer, blobName).DeleteIfExistsAsync();
        }
        return Results.NoContent();
    }

    /// <summary>
    /// Changes a Release's Track list while holding a lock on the Release, so concurrent uploads can't get past the
    /// Track limit or share a Track number. Answers "not found" to anyone who doesn't manage the Artist, and refuses
    /// a Release that can't be edited. <paramref name="change"/> returns null when it succeeded.
    /// </summary>
    private static Task<IResult?> WithReleaseLockedAsync(
        KleinGeldDbContext db, Guid id, ClaimsPrincipal caller, Func<Release, Task<IResult?>> change) =>
        // Retries run the whole transaction again, so nothing tracked by a failed attempt may carry over.
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlAsync($"""SELECT 1 FROM "Releases" WHERE "Id" = {id} FOR UPDATE""");

            var release = await db.Releases
                .Include(r => r.Tracks)
                .Where(r => r.Id == id)
                .ManagedBy(caller.MemberId())
                .SingleOrDefaultAsync();
            if (release is null)
            {
                return Results.NotFound();
            }
            if (release.Stage is not (ReleaseStage.Draft or ReleaseStage.Rejected))
            {
                return Results.Problem(
                    "Tracks can only be changed on a Draft or Rejected Release.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            var refused = await change(release);
            if (refused is null)
            {
                await transaction.CommitAsync();
            }
            return refused;
        });

    private static string TitleFrom(string fileName)
    {
        var title = Path.GetFileNameWithoutExtension(fileName.Trim());
        title = string.IsNullOrWhiteSpace(title) ? fileName.Trim() : title;
        return title.Length > ReleaseForm.TitleMaxLength ? title[..ReleaseForm.TitleMaxLength] : title;
    }

    private static TrackSummary ToSummary(Track track) => new(track.Id, track.Title, track.Position, track.Status, track.Duration);
}
