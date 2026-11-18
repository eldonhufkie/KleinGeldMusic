namespace KleinGeld.Contracts;

/// <summary>
/// A Release, as returned by <c>GET /api/releases/{id}/{slug}</c>. <paramref name="PendingChanges"/> is only given to
/// its managers and the Admin.
/// </summary>
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
    DateTimeOffset? PublishedAt = null,
    string? RejectionReason = null,
    ReleaseStage? PendingChanges = null,
    string? PendingChangesRejectionReason = null);
