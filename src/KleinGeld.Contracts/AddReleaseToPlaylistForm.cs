namespace KleinGeld.Contracts;

/// <summary>
/// The Release whose Tracks to append to a Playlist, as posted to <c>POST /api/playlists/{id}/releases</c>.
/// Tracks already on the Playlist are skipped.
/// </summary>
public sealed class AddReleaseToPlaylistForm
{
    public Guid ReleaseId { get; set; }
}

/// <summary>How many of the Release's Tracks were appended, and how many were already on the Playlist.</summary>
public sealed record AddReleaseToPlaylistResult(int Added, int Skipped, PlaylistSummary Playlist);
