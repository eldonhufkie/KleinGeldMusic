namespace KleinGeld.Contracts;

/// <summary>What one search finds: the top 5 of each group. A group with a <c>Next</c> has more under Show all.</summary>
public sealed record SearchResults(
    SearchPage<ArtistCard> Artists,
    SearchPage<SearchRelease> Releases,
    SearchPage<SearchTrack> Tracks);

/// <summary>Search results of one group, best match first. <see cref="Next"/> asks for more; null when there are no more.</summary>
public sealed record SearchPage<T>(IReadOnlyList<T> Items, string? Next);

/// <summary>A Published Release found by its title.</summary>
public sealed record SearchRelease(Guid Id, string Title, string Slug, ReleaseType Type, string ArtistName, CoverArt? Cover);

/// <summary>A Track of a Published Release, found by its title or its featured Artists.</summary>
public sealed record SearchTrack(
    Guid Id,
    string Title,
    string? FeaturedArtists,
    Guid ReleaseId,
    string ReleaseTitle,
    string ReleaseSlug,
    string ArtistName,
    CoverArt? Cover);
