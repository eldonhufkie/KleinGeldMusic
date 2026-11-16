namespace KleinGeld.Contracts;

/// <summary>
/// The public Artist page, as returned by <c>GET /api/artists/{id}/{slug}</c>: only Published Releases, whoever asks.
/// Each section is newest release date first, and <paramref name="Newest"/> is what the page's Play button plays.
/// </summary>
public sealed record ArtistDetails(
    Guid Id,
    string Name,
    string Slug,
    ArtistRelease? Newest,
    IReadOnlyList<ArtistRelease> Albums,
    IReadOnlyList<ArtistRelease> EPs,
    IReadOnlyList<ArtistRelease> Singles);

/// <summary>A Published Release in a section of the public Artist page.</summary>
public sealed record ArtistRelease(Guid Id, string Title, string Slug, DateOnly? ReleaseDate, CoverArt? Cover);
