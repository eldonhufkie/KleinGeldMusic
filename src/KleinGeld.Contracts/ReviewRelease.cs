namespace KleinGeld.Contracts;

/// <summary>
/// A Submitted Release, or the Submitted Pending Changes of a Published one, on the Admin's Review page, as returned
/// by <c>GET /api/admin/review</c>. For Pending Changes, <paramref name="AddedTracks"/> is all that changed.
/// </summary>
public sealed record ReviewRelease(
    Guid Id,
    string Title,
    string Slug,
    string ArtistName,
    ReleaseType Type,
    DateTimeOffset SubmittedAt,
    IReadOnlyList<MadeRightsDeclaration> RightsDeclarations,
    bool IsPendingChanges = false,
    IReadOnlyList<TrackSummary>? AddedTracks = null);

/// <summary>One Rights Declaration: who made it, when, and which wording they ticked. Newest first in a list.</summary>
public sealed record MadeRightsDeclaration(string? MemberName, DateTimeOffset MadeAt, int WordingVersion);
