using System.Security.Claims;
using KleinGeld.Contracts;
using KleinGeld.Data;
using Microsoft.EntityFrameworkCore;

namespace KleinGeld.Web.Api;

internal static class PlaylistEndpoints
{
    /// <summary>
    /// A Member's own Playlists. They are private: anyone else, the Admin included, gets "not found".
    /// </summary>
    public static void MapPlaylistEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/playlists", async (PlaylistForm form, ClaimsPrincipal caller, KleinGeldDbContext db, TimeProvider clock) =>
        {
            var name = form.Name.Trim();
            var playlist = new Playlist
            {
                MemberId = caller.MemberId(),
                Name = name,
                Slug = Slugs.From(name),
                ChangedAt = clock.GetUtcNow(),
            };
            db.Playlists.Add(playlist);
            await db.SaveChangesAsync();

            return Results.Created($"/api/playlists/{playlist.Id}/{playlist.Slug}", ToSummary(playlist, 0));
        });

        api.MapGet("/playlists", async (ClaimsPrincipal caller, KleinGeldDbContext db) =>
        {
            var memberId = caller.MemberId();
            var rows = await db.Playlists
                .Where(p => p.MemberId == memberId)
                .OrderByDescending(p => p.ChangedAt)
                .ThenByDescending(p => p.Id)
                .Select(p => new { Playlist = p, Count = p.Entries.Count })
                .ToListAsync();
            return rows.Select(r => ToSummary(r.Playlist, r.Count));
        });

        // A wrong or old slug redirects, like a Release's.
        api.MapGet("/playlists/{id:guid}/{slug?}", async (Guid id, string? slug, ClaimsPrincipal caller, KleinGeldDbContext db) =>
        {
            var memberId = caller.MemberId();
            var playlist = await db.Playlists.SingleOrDefaultAsync(p => p.Id == id && p.MemberId == memberId);
            if (playlist is null)
            {
                return Results.NotFound();
            }
            if (slug != playlist.Slug)
            {
                return Results.Redirect($"/api/playlists/{playlist.Id}/{playlist.Slug}");
            }

            var entries = await db.Set<PlaylistEntry>()
                .Where(e => e.PlaylistId == id)
                .OrderBy(e => e.Position)
                .Select(e => new
                {
                    e.TrackId,
                    e.Title,
                    e.ArtistName,
                    Track = e.Track == null ? null : new
                    {
                        e.Track.Title,
                        // A replaced Track plays its live audio for Members until the Pending Changes are approved.
                        Duration = e.Track.LiveConvertedBlobName != null ? e.Track.LiveDuration : e.Track.Duration,
                        e.Track.SingleMark,
                        e.Track.FeaturedArtists,
                        e.Track.Explicit,
                        Ready = e.Track.LiveConvertedBlobName != null || e.Track.Status == TrackStatus.Ready,
                        e.Track.Release.ArtistId,
                        ArtistName = e.Track.Release.Artist.Name,
                        ReleaseId = e.Track.Release.Id,
                        ReleaseTitle = e.Track.Release.Title,
                        ReleaseSlug = e.Track.Release.Slug,
                        Published = e.Track.Release.Stage == ReleaseStage.Published,
                    },
                })
                .ToListAsync();

            var tracks = entries
                .Select(e => e.Track is { Published: true } t
                    ? new PlaylistTrack(
                        e.TrackId, t.Title, t.ArtistName, t.Ready, t.ArtistId, t.ReleaseId,
                        t.ReleaseTitle, t.ReleaseSlug, t.Duration, t.SingleMark, t.FeaturedArtists, t.Explicit)
                    : new PlaylistTrack(e.TrackId, e.Title, e.ArtistName, IsAvailable: false))
                .ToList();
            var total = TimeSpan.FromTicks(tracks.Where(t => t.IsAvailable).Sum(t => t.Duration?.Ticks ?? 0));

            return Results.Ok(new PlaylistDetails(playlist.Id, playlist.Name, playlist.Slug, playlist.ChangedAt, tracks, total));
        });

        api.MapPost("/playlists/{id:guid}/tracks", async (
            Guid id, AddToPlaylistForm form, ClaimsPrincipal caller, KleinGeldDbContext db, TimeProvider clock) =>
        {
            var memberId = caller.MemberId();
            var playlist = await db.Playlists
                .Include(p => p.Entries)
                .SingleOrDefaultAsync(p => p.Id == id && p.MemberId == memberId);
            // Only a Track the Member may see can be added, so a Playlist reveals nothing unpublished.
            var track = await db.Releases
                .VisibleTo(caller)
                .SelectMany(r => r.Tracks, (release, t) => new { t.Id, t.Title, ArtistName = release.Artist.Name })
                .SingleOrDefaultAsync(t => t.Id == form.TrackId);
            if (playlist is null || track is null)
            {
                return Results.NotFound();
            }
            if (playlist.Entries.Any(e => e.TrackId == track.Id))
            {
                return Results.Problem($"Already on {playlist.Name}.", statusCode: StatusCodes.Status409Conflict);
            }

            // Added explicitly: the key is set client-side, so via the navigation EF would take it for an existing row.
            db.Add(new PlaylistEntry
            {
                PlaylistId = playlist.Id,
                TrackId = track.Id,
                Position = playlist.Entries.Count + 1,
                Title = track.Title,
                ArtistName = track.ArtistName,
            });
            playlist.ChangedAt = clock.GetUtcNow();
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // The same Track added at the same moment from another tab.
                return Results.Problem($"Already on {playlist.Name}.", statusCode: StatusCodes.Status409Conflict);
            }

            return Results.Ok(ToSummary(playlist, playlist.Entries.Count));
        });
    }

    private static PlaylistSummary ToSummary(Playlist playlist, int trackCount) =>
        new(playlist.Id, playlist.Name, playlist.Slug, trackCount, playlist.ChangedAt);
}
