using KleinGeld.Contracts;
using KleinGeld.Data;
using KleinGeld.Web.Blobs;
using Microsoft.EntityFrameworkCore;

namespace KleinGeld.Web.Api;

/// <summary>
/// What a removal left to clean up once committed: the Releases whose covers go, the audio blobs that go, and whether
/// anything Published went, so play links already handed out must stop working.
/// </summary>
internal sealed record Removed(IReadOnlyList<Guid> ReleaseIds, IReadOnlyList<string> BlobNames, bool AnyPublished)
{
    public static readonly Removed Nothing = new([], [], false);
}

/// <summary>
/// The one way a Release or an Artist is deleted, by its managing Members or by the Admin: removed inside the caller's
/// lock and transaction, then cleaned up after the commit.
/// </summary>
internal static class ContentRemoval
{
    /// <summary>Removes a Release, with all its Tracks and any Pending Changes; the caller holds its lock.</summary>
    public static async Task<Removed> RemoveReleaseAsync(this KleinGeldDbContext db, Release release)
    {
        var removed = new Removed(
            [release.Id],
            release.Tracks.SelectMany(t => t.BlobNames()).ToList(),
            release.Stage == ReleaseStage.Published);
        db.Releases.Remove(release);
        await db.SaveChangesAsync();
        return removed;
    }

    /// <summary>Removes an Artist with all its Releases; the caller holds the Artist's lock.</summary>
    public static async Task<Removed> RemoveArtistAsync(this KleinGeldDbContext db, Artist artist)
    {
        var releases = await db.Releases.Include(r => r.Tracks).Where(r => r.ArtistId == artist.Id).ToListAsync();
        var removed = new Removed(
            releases.Select(r => r.Id).ToList(),
            releases.SelectMany(r => r.Tracks).SelectMany(t => t.BlobNames()).ToList(),
            releases.Any(r => r.Stage == ReleaseStage.Published));
        db.Artists.Remove(artist);
        await db.SaveChangesAsync();
        return removed;
    }
}

/// <summary>The stores a removal cleans up after its commit, bundled for the endpoints that take them.</summary>
internal sealed record RemovalStores(CoverStore Covers, BlobLinks Links, PlayLinks PlayLinks)
{
    /// <summary>
    /// Runs after the commit, so a link fetched meanwhile is either refused or signed against the policy that goes.
    /// </summary>
    public async Task CleanUpAsync(Removed removed)
    {
        if (removed.AnyPublished)
        {
            await PlayLinks.RevokeAllAsync();
        }

        foreach (var releaseId in removed.ReleaseIds)
        {
            await Covers.DeleteAllAsync(releaseId);
        }

        foreach (var blobName in removed.BlobNames)
        {
            await Links.Blob(TrackEndpoints.AudioContainer, blobName).DeleteIfExistsAsync();
        }
    }
}
