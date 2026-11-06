namespace KleinGeld.Contracts;

/// <summary>A Release as shown on a cover grid, with what it takes to link to it.</summary>
public sealed record ReleaseCard(
    Guid Id,
    string Title,
    string Slug,
    ReleaseType Type,
    string ArtistName,
    DateTimeOffset PublishedAt,
    Uri? Cover);
