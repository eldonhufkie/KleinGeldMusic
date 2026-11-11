using System.Security.Claims;
using KleinGeld.Contracts;
using KleinGeld.Data;
using KleinGeld.Web.Blobs;
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
        api.MapGet("/playlists/{id:guid}/{slug?}", async (Guid id, string? slug, ClaimsPrincipal caller, KleinGeldDbContext db, CoverStore covers) =>
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
                    e.Id,
                    e.TrackId,
                    e.Title,
                    e.ArtistName,
                    Track = e.Track == null ? null : new
                    {
                        e.Track.Title,
                        e.Track.Duration,
                        e.Track.SingleMark,
                        e.Track.FeaturedArtists,
                        e.Track.Explicit,
                        e.Track.Status,
                        e.Track.Release.ArtistId,
                        ArtistName = e.Track.Release.Artist.Name,
                        ReleaseId = e.Track.Release.Id,
                        ReleaseTitle = e.Track.Release.Title,
                        ReleaseSlug = e.Track.Release.Slug,
                        Published = e.Track.Release.Stage == ReleaseStage.Published,
                        e.Track.Release.CoverId,
                    },
                })
                .ToListAsync();

            var tracks = entries
                .Select(e => e.Track is { Published: true } t
                    ? new PlaylistTrack(
                        e.TrackId, t.Title, t.ArtistName, t.Status == TrackStatus.Ready, t.ArtistId, t.ReleaseId,
                        t.ReleaseTitle, t.ReleaseSlug, t.Duration, t.SingleMark, t.FeaturedArtists, t.Explicit) { EntryId = e.Id }
                    : new PlaylistTrack(e.TrackId, e.Title, e.ArtistName, IsAvailable: false) { EntryId = e.Id })
                .ToList();
            var total = TimeSpan.FromTicks(tracks.Where(t => t.IsAvailable).Sum(t => t.Duration?.Ticks ?? 0));

            // The cover is the cover of the first playable Track's Release.
            var coverOf = entries.FirstOrDefault(e => e.Track is { Published: true, Status: TrackStatus.Ready })?.Track;
            return Results.Ok(new PlaylistDetails(playlist.Id, playlist.Name, playlist.Slug, playlist.ChangedAt, tracks, total)
            {
                Cover = coverOf?.CoverId is { } coverId ? covers.LinksTo(coverOf.ReleaseId, coverId) : null,
            });
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

        api.MapPut("/playlists/{id:guid}", async (Guid id, PlaylistForm form, ClaimsPrincipal caller, KleinGeldDbContext db, TimeProvider clock) =>
        {
            var playlist = await FindOwnAsync(db, id, caller);
            if (playlist is null)
            {
                return Results.NotFound();
            }
            playlist.Name = form.Name.Trim();
            playlist.Slug = Slugs.From(playlist.Name);
            playlist.ChangedAt = clock.GetUtcNow();
            await db.SaveChangesAsync();
            return Results.Ok(ToSummary(playlist, playlist.Entries.Count));
        });

        // Permanent: the entries go with it. A Queue built from it keeps its own copy of the Tracks.
        api.MapDelete("/playlists/{id:guid}", async (Guid id, ClaimsPrincipal caller, KleinGeldDbContext db) =>
        {
            var playlist = await FindOwnAsync(db, id, caller);
            if (playlist is null)
            {
                return Results.NotFound();
            }
            db.Playlists.Remove(playlist);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        api.MapPut("/playlists/{id:guid}/entries/{entryId:guid}/position", async (
            Guid id, Guid entryId, MovePlaylistEntryForm form, ClaimsPrincipal caller, KleinGeldDbContext db, TimeProvider clock) =>
        {
            var playlist = await FindOwnAsync(db, id, caller);
            var entries = playlist?.Entries.OrderBy(e => e.Position).ToList();
            var entry = entries?.SingleOrDefault(e => e.Id == entryId);
            if (playlist is null || entry is null)
            {
                return Results.NotFound();
            }
            entries!.Remove(entry);
            entries.Insert(Math.Clamp(form.Position, 1, entries.Count + 1) - 1, entry);
            Renumber(entries);
            playlist.ChangedAt = clock.GetUtcNow();
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        api.MapDelete("/playlists/{id:guid}/entries/{entryId:guid}", async (
            Guid id, Guid entryId, ClaimsPrincipal caller, KleinGeldDbContext db, TimeProvider clock) =>
        {
            var playlist = await FindOwnAsync(db, id, caller);
            var entries = playlist?.Entries.OrderBy(e => e.Position).ToList();
            var entry = entries?.SingleOrDefault(e => e.Id == entryId);
            if (playlist is null || entry is null)
            {
                return Results.NotFound();
            }
            entries!.Remove(entry);
            db.Remove(entry);
            Renumber(entries);
            playlist.ChangedAt = clock.GetUtcNow();
            await db.SaveChangesAsync();
            return Results.Ok(new RemovedPlaylistEntry(entry.TrackId, entry.Title, entry.ArtistName, entry.Position));
        });

        // The Undo of a removal: the entry goes back in the place it had, with the snapshot it had.
        api.MapPost("/playlists/{id:guid}/entries/restore", async (
            Guid id, RemovedPlaylistEntry removed, ClaimsPrincipal caller, KleinGeldDbContext db, TimeProvider clock) =>
        {
            var playlist = await FindOwnAsync(db, id, caller);
            if (playlist is null)
            {
                return Results.NotFound();
            }
            // A Track that still exists must still be one the Member may see.
            if (removed.TrackId is { } trackId &&
                !await db.Releases.VisibleTo(caller).SelectMany(r => r.Tracks).AnyAsync(t => t.Id == trackId))
            {
                return Results.NotFound();
            }
            if (removed.TrackId is not null && playlist.Entries.Any(e => e.TrackId == removed.TrackId))
            {
                return Results.Problem($"Already on {playlist.Name}.", statusCode: StatusCodes.Status409Conflict);
            }

            var entries = playlist.Entries.OrderBy(e => e.Position).ToList();
            // Added explicitly: the key is set client-side, so via the navigation EF would take it for an existing row.
            var entry = new PlaylistEntry
            {
                PlaylistId = playlist.Id,
                TrackId = removed.TrackId,
                Position = 0,
                Title = removed.Title,
                ArtistName = removed.ArtistName,
            };
            db.Add(entry);
            entries.Insert(Math.Clamp(removed.Position, 1, entries.Count + 1) - 1, entry);
            Renumber(entries);
            playlist.ChangedAt = clock.GetUtcNow();
            await db.SaveChangesAsync();
            return Results.Ok(ToSummary(playlist, entries.Count));
        });
    }

    private static Task<Playlist?> FindOwnAsync(KleinGeldDbContext db, Guid id, ClaimsPrincipal caller)
    {
        var memberId = caller.MemberId();
        return db.Playlists.Include(p => p.Entries).SingleOrDefaultAsync(p => p.Id == id && p.MemberId == memberId);
    }

    private static void Renumber(List<PlaylistEntry> entries)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            entries[i].Position = i + 1;
        }
    }

    private static PlaylistSummary ToSummary(Playlist playlist, int trackCount) =>
        new(playlist.Id, playlist.Name, playlist.Slug, trackCount, playlist.ChangedAt);
}
