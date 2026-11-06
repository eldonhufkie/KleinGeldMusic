namespace KleinGeld.Contracts;

/// <summary>
/// A Track on a Release's Track list, as returned by <c>GET /api/tracks?releaseId={id}</c>.
/// <paramref name="Duration"/> is known once the Track is Ready.
/// </summary>
public sealed record TrackSummary(
    Guid Id,
    string Title,
    int Position,
    TrackStatus Status,
    bool SingleMark = false,
    string? FeaturedArtists = null,
    bool Explicit = false,
    TimeSpan? Duration = null);
