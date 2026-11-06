namespace KleinGeld.Contracts;

/// <summary>One load of the Artists grid, as returned by <c>GET /api/artists?skip={n}</c>.</summary>
public sealed record ArtistList(IReadOnlyList<ArtistCard> Artists, bool HasMore);

/// <summary>An Artist in the grid, with the cover of its newest Published Release.</summary>
public sealed record ArtistCard(Guid Id, string Name, string Slug, CoverArt? Cover);
