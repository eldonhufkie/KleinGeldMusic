namespace KleinGeld.Contracts;

/// <summary>A Release, as returned by <c>GET /api/releases/{id}/{slug}</c>.</summary>
public sealed record ReleaseDetails(
    Guid Id,
    Guid ArtistId,
    string ArtistName,
    string Title,
    string Slug,
    ReleaseType Type,
    DateOnly? ReleaseDate,
    string? Description,
    string? Genre,
    ReleaseStage Stage,
    CoverArt? Cover = null,
    DateTimeOffset? PublishedAt = null);
