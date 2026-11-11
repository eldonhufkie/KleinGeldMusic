namespace KleinGeld.Contracts;

/// <summary>A Playlist with its Tracks in order, as returned by <c>GET /api/playlists/{id}/{slug}</c>.</summary>
public sealed record PlaylistDetails(
    Guid Id,
    string Name,
    string Slug,
    DateTimeOffset ChangedAt,
    IReadOnlyList<PlaylistTrack> Tracks,
    TimeSpan TotalDuration);

/// <summary>
/// One entry on a Playlist. <paramref name="Title"/> and <paramref name="ArtistName"/> are the snapshot taken when it
/// was added; the links and the Track's details are only there while the Track is playable.
/// </summary>
public sealed record PlaylistTrack(
    Guid? TrackId,
    string Title,
    string ArtistName,
    bool IsAvailable,
    Guid? ArtistId = null,
    Guid? ReleaseId = null,
    string? ReleaseTitle = null,
    string? ReleaseSlug = null,
    TimeSpan? Duration = null,
    bool SingleMark = false,
    string? FeaturedArtists = null,
    bool Explicit = false);
