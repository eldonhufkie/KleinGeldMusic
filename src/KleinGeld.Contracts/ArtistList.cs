namespace KleinGeld.Contracts;

/// <summary>One page of the Artists grid. <see cref="Next"/> asks for the following page; null on the last one.</summary>
public sealed record ArtistList(IReadOnlyList<ArtistCard> Artists, string? Next);

/// <summary>An Artist in the grid, with the cover of its newest Published Release.</summary>
public sealed record ArtistCard(Guid Id, string Name, string Slug, CoverArt? Cover);
